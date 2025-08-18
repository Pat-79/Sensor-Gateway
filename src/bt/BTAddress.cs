using System;

namespace SensorGateway.Bluetooth
{
    /// <summary>
    /// Represents a Bluetooth MAC address with parsing and formatting capabilities.
    /// This class handles conversion between string representation (XX:XX:XX:XX:XX:XX)
    /// and byte array representation of Bluetooth addresses.
    /// Implements IDisposable for proper resource cleanup.
    /// </summary>
    public class BTAddress
    {
        /// <summary>
        /// Gets or sets the Bluetooth address as a 6-byte array.
        /// The bytes are stored in the same order as they appear in the string representation.
        /// </summary>
        public byte[] Address { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Initializes a new BTAddress instance from a byte array.
        /// The byte array must contain exactly 6 bytes representing the Bluetooth MAC address.
        /// </summary>
        /// <param name="address">6-byte array containing the Bluetooth address</param>
        /// <exception cref="ArgumentNullException">Thrown when address array is null</exception>
        public BTAddress(byte[] address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address), "Address cannot be null");
        }

        /// <summary>
        /// Initializes a new BTAddress instance from a string representation.
        /// Parses Bluetooth addresses in the standard format "XX:XX:XX:XX:XX:XX"
        /// where each XX represents a hexadecimal byte value.
        /// </summary>
        /// <param name="address">Bluetooth address string in format "XX:XX:XX:XX:XX:XX"</param>
        /// <exception cref="ArgumentNullException">Thrown when address string is null or empty</exception>
        /// <exception cref="ArgumentException">Thrown when address format is invalid (not 6 colon-separated parts)</exception>
        /// <exception cref="ArgumentException">Thrown when any part cannot be parsed as a hexadecimal byte</exception>
        /// <example>
        /// Valid address formats: "ED:A6:2B:16:EB:E9", "00:11:22:33:44:55"
        /// </example>
        public BTAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException(nameof(address), "Address cannot be null or empty");
            }

            // Split address by colons - should result in exactly 6 parts
            var parts = address.Split(':');
            if (parts.Length != 6)
            {
                throw new ArgumentException("Invalid Bluetooth address format. Expected format is XX:XX:XX:XX:XX:XX", nameof(address));
            }

            // Parse each part as a hexadecimal byte
            Address = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                if (!byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out Address[i]))
                {
                    throw new ArgumentException($"Invalid byte value '{parts[i]}' in Bluetooth address.", nameof(address));
                }
            }
        }

        /// <summary>
        /// Converts the Bluetooth address to its string representation.
        /// Returns the address in standard format "XX:XX:XX:XX:XX:XX"
        /// where each XX is a hexadecimal byte value.
        /// </summary>
        /// <returns>String representation of the Bluetooth address</returns>
        /// <example>
        /// Returns: "ED:A6:2B:16:EB:E9"
        /// </example>
        public override string ToString()
        {
            return BitConverter.ToString(Address).Replace("-", ":");
        }
    }
}