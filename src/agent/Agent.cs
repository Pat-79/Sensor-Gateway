using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HashtagChris.DotNetBlueZ;
using HashtagChris.DotNetBlueZ.Extensions;
using SensorGateway.Configuration;
using Tmds.DBus;

namespace SensorGateway.Bluetooth.Agent
{
    /// <summary>
    /// Singleton Bluetooth D-Bus agent that handles authentication requests
    /// </summary>
    public sealed class BluetoothAgent : IDisposable
    {
        private static readonly Lazy<BluetoothAgent> _instance = new(() => new BluetoothAgent());
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundAgentTask;
        private readonly SemaphoreSlim _registrationSemaphore;
        private readonly object _handlerLock = new();
        
        private IBluetoothAuthenticationHandler _authHandler;
        private IAgentManager1? _agentManager;
        private Connection? _dbusConnection;
        private volatile bool _disposed = false;
        private volatile bool _isRegistered = false;
        
        // Configuration
        private readonly string _agentPath;
        private readonly string _agentCapability;
        private readonly TimeSpan _reregistrationInterval;

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static BluetoothAgent Instance => _instance.Value;

        /// <summary>
        /// Event fired when the agent is registered
        /// </summary>
        public event EventHandler<AgentRegisteredEventArgs>? AgentRegistered;

        /// <summary>
        /// Event fired when the agent is unregistered
        /// </summary>
        public event EventHandler<AgentUnregisteredEventArgs>? AgentUnregistered;

        private BluetoothAgent()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _registrationSemaphore = new SemaphoreSlim(1, 1);
            
            var bluetoothConfig = AppConfig.Bluetooth;
            var agentConfig = bluetoothConfig.Agent; // Get nested agent config
            
            _agentPath = agentConfig.AgentPath;
            _agentCapability = agentConfig.AgentCapability;
            _reregistrationInterval = TimeSpan.FromSeconds(agentConfig.ReregistrationIntervalSeconds);
            
            // Initialize with agent config (not bluetooth config)
            _authHandler = new FixedPinAuthenticationHandler(agentConfig);
            
            Console.WriteLine($"🤖 BluetoothAgent initializing at path: {_agentPath}");
            Console.WriteLine($"🤖 Agent capability: {_agentCapability}");
            
            // Start background agent thread
            _backgroundAgentTask = Task.Run(AgentBackgroundLoop, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Sets the authentication handler (dependency injection)
        /// </summary>
        /// <param name="handler">The authentication handler to use</param>
        public void SetAuthenticationHandler(IBluetoothAuthenticationHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            
            lock (_handlerLock)
            {
                _authHandler = handler;
                Console.WriteLine($"🔄 Authentication handler updated: {handler.GetType().Name}");
            }
        }

        /// <summary>
        /// Gets the current registration status
        /// </summary>
        public bool IsRegistered => _isRegistered && !_disposed;

        /// <summary>
        /// Background loop that manages agent registration and re-registration
        /// </summary>
        private async Task AgentBackgroundLoop()
        {
            Console.WriteLine("🚀 BluetoothAgent background thread started");
            
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Ensure we're registered as the agent
                        await EnsureAgentRegistrationAsync();
                        
                        // Wait before next check
                        await Task.Delay(_reregistrationInterval, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Expected during shutdown
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error in BluetoothAgent background loop: {ex.Message}");
                        // Wait longer before retrying after error
                        await Task.Delay(TimeSpan.FromMinutes(1), _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            
            Console.WriteLine("🛑 BluetoothAgent background thread stopped");
        }

        /// <summary>
        /// Ensures the agent is registered, re-registering if necessary
        /// </summary>
        private async Task EnsureAgentRegistrationAsync()
        {
            if (_disposed)
                return;

            await _registrationSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                // Check if we need to register or re-register
                if (!_isRegistered || ShouldReregister())
                {
                    await RegisterAgentAsync();
                }
            }
            finally
            {
                _registrationSemaphore.Release();
            }
        }

        /// <summary>
        /// Checks if the agent needs to be re-registered
        /// </summary>
        private bool ShouldReregister()
        {
            try
            {
                if (_agentManager == null)
                    return true;

                // Try to check if we're still the default agent
                // This is implementation-specific and might need adjustment
                // based on available D-Bus methods
                return false; // For now, assume we don't need to re-register unless explicitly told
            }
            catch
            {
                return true; // Re-register if we can't verify status
            }
        }

        /// <summary>
        /// Registers the agent with BlueZ
        /// </summary>
        private async Task RegisterAgentAsync()
        {
            try
            {
                Console.WriteLine("🔄 Registering BluetoothAgent...");
                
                // Connect to D-Bus if not already connected
                if (_dbusConnection == null)
                {
                    _dbusConnection = Connection.System;
                }

                // Get the agent manager
                _agentManager = _dbusConnection.CreateProxy<IAgentManager1>("org.bluez", "/org/bluez");

                // Create and register our agent object
                var agentObject = new BluetoothAgentObject(this, _authHandler);
                await _dbusConnection.RegisterObjectAsync(agentObject);

                // Register with BlueZ
                await _agentManager.RegisterAgentAsync(_agentPath, _agentCapability);
                Console.WriteLine($"✅ Agent registered at {_agentPath} with capability '{_agentCapability}'");

                // Request to be the default agent
                try
                {
                    await _agentManager.RequestDefaultAgentAsync(_agentPath);
                    Console.WriteLine("✅ Agent set as default");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Could not set as default agent: {ex.Message}");
                    // Not critical - we can still handle requests even if not default
                }

                _isRegistered = true;
                
                // Fire event
                AgentRegistered?.Invoke(this, new AgentRegisteredEventArgs(_agentPath, _agentCapability));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to register BluetoothAgent: {ex.Message}");
                _isRegistered = false;
                throw;
            }
        }

        /// <summary>
        /// Unregisters the agent
        /// </summary>
        private async Task UnregisterAgentAsync()
        {
            if (!_isRegistered || _agentManager == null)
                return;

            try
            {
                Console.WriteLine("🔄 Unregistering BluetoothAgent...");
                await _agentManager.UnregisterAgentAsync(_agentPath);
                Console.WriteLine("✅ Agent unregistered");
                
                _isRegistered = false;
                
                // Fire event
                AgentUnregistered?.Invoke(this, new AgentUnregisteredEventArgs(_agentPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error unregistering agent: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the agent is released by BlueZ (external unregistration)
        /// </summary>
        internal void OnAgentReleased()
        {
            Console.WriteLine("⚠️ BluetoothAgent was released externally - will re-register");
            _isRegistered = false;
            
            // Fire event
            AgentUnregistered?.Invoke(this, new AgentUnregisteredEventArgs(_agentPath, wasExternal: true));
        }

        /// <summary>
        /// Gets the authentication handler (thread-safe)
        /// </summary>
        internal IBluetoothAuthenticationHandler GetAuthenticationHandler()
        {
            lock (_handlerLock)
            {
                return _authHandler;
            }
        }

        /// <summary>
        /// Disposes the agent and stops background operations
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Console.WriteLine("🛑 Shutting down BluetoothAgent...");
                
                _disposed = true;
                
                // Clear event handlers
                AgentRegistered = null;
                AgentUnregistered = null;
                
                // Cancel background thread
                _cancellationTokenSource.Cancel();
                
                try
                {
                    // Wait for background thread to complete
                    _backgroundAgentTask.Wait(TimeSpan.FromSeconds(5));
                    
                    // Unregister agent
                    UnregisterAgentAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error during BluetoothAgent shutdown: {ex.Message}");
                }
                
                // Dispose resources
                _cancellationTokenSource.Dispose();
                _registrationSemaphore.Dispose();
                
                Console.WriteLine("✅ BluetoothAgent shutdown complete");
            }
        }
    }
}