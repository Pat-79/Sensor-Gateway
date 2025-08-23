using System;
using System.Collections.Generic;
using System.Linq;

namespace SensorGateway.Gateway
{
    #region Measurement Source enum
    /// <summary>
    /// Source of sensor measurements
    /// </summary>
    public enum MeasurementSource
    {
        Advertisement,
        Log,
        Both
    }
    #endregion

    #region Measurement Type enum
    /// <summary>
    /// Types of measurements that can be taken by sensors
    /// </summary>
    public enum MeasurementType
    {
        Temperature,
        Battery
        /* more can be implemented later */
    }
    #endregion

    #region Measurement class
    /// <summary>
    /// Represents a single measurement from the sensor
    /// </summary>
    public class Measurement
    {
        /// <summary>
        /// The type of measurement
        /// </summary>
        public MeasurementType Type { get; set; }

        /// <summary>
        /// The measured value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// The unit of measurement (e.g., "°C", "V", "mV", "%", "lux")
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// The UTC datetime when the measurement was taken
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The source of the measurement (log or advertisement)
        /// </summary>
        public MeasurementSource Source { get; set; } = MeasurementSource.Both;

        /// <summary>
        /// Optional measurement ID associated with the measurement
        /// </summary>
        public ulong? ID { get; set; }
    }
    #endregion

    #region Measurement Type Extensions
    /// <summary>
    /// Helper extension methods for working with measurements
    /// </summary>
    public static class MeasurementExtensions
    {
        /// <summary>
        /// Filters measurements by type
        /// </summary>
        public static IEnumerable<Measurement> OfType(this IEnumerable<Measurement> measurements, MeasurementType type)
        {
            return measurements.Where(m => m.Type == type);
        }

        /// <summary>
        /// Gets the latest measurement of a specific type
        /// </summary>
        public static Measurement? GetLatest(this IEnumerable<Measurement> measurements, MeasurementType type)
        {
            return measurements.OfType(type).OrderByDescending(m => m.TimestampUtc).FirstOrDefault();
        }

        /// <summary>
        /// Gets the latest temperature measurement value
        /// </summary>
        public static double? GetLatestTemperature(this IEnumerable<Measurement> measurements)
        {
            return measurements.GetLatest(MeasurementType.Temperature)?.Value;
        }

        /// <summary>
        /// Gets the latest battery measurement value
        /// </summary>
        public static double? GetLatestBattery(this IEnumerable<Measurement> measurements)
        {
            return measurements.GetLatest(MeasurementType.Battery)?.Value;
        }
    }
    #endregion
}