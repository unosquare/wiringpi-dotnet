namespace Unosquare.WiringPI
{
    using RaspberryIO.Abstractions;

    /// <summary>
    /// Represents the Bootstrap class to extract resources.
    /// </summary>
    /// <seealso cref="Unosquare.RaspberryIO.Abstractions.IBootstrap" />
    public class BootstrapWiringPi : IBootstrap
    {
        private static readonly object SyncLock = new object();

        /// <inheritdoc />
        public void Bootstrap()
        {
            lock (SyncLock)
            {
                Resources.EmbeddedResources.ExtractAll();
            }
        }
    }
}
