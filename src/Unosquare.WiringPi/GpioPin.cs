namespace Unosquare.WiringPi
{
    using System;
    using System.Threading.Tasks;
    using Native;
    using RaspberryIO.Abstractions;
    using RaspberryIO.Abstractions.Native;
    using Swan.Diagnostics;
    using Definitions = RaspberryIO.Abstractions.Definitions;

    /// <summary>
    /// Represents a GPIO Pin, its location and its capabilities.
    /// Full pin reference available here:
    /// http://pinout.xyz/pinout/pin31_gpio6 and  http://wiringpi.com/pins/.
    /// </summary>
    public sealed partial class GpioPin : IGpioPin
    {
        #region Property Backing

        private static readonly int[] GpioToWiringPi;

        private static readonly int[] GpioToWiringPiR1 =
        {
            8, 9, -1, -1, 7, -1, -1, 11, 10, 13, 12, 14, -1, -1, 15, 16, -1, 0, 1, -1, -1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1,
        };

        private static readonly int[] GpioToWiringPiR2 =
        {
            30, 31, 8, 9, 7, 21, 22, 11, 10, 13, 12, 14, 26, 23, 15, 16, 27, 0, 1, 24, 28, 29, 3, 4, 5, 6, 25, 2, 17, 18, 19, 20,
        };

        private readonly object _syncLock = new object();
        private GpioPinDriveMode _pinMode;
        private GpioPinResistorPullMode _resistorPullMode;
        private int _pwmRegister;
        private PwmMode _pwmMode = PwmMode.Balanced;
        private uint _pwmRange = 1024;
        private int _pwmClockDivisor = 1;
        private int _softPwmValue = -1;
        private int _softToneFrequency = -1;

        #endregion

        #region Constructor

        static GpioPin()
        {
            GpioToWiringPi = SystemInfo.GetBoardRevision() ==
                BoardRevision.Rev1 ? GpioToWiringPiR1 : GpioToWiringPiR2;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpioPin"/> class.
        /// </summary>
        /// <param name="bcmPinNumber">The BCM pin number.</param>
        private GpioPin(BcmPin bcmPinNumber)
        {
            BcmPin = bcmPinNumber;
            BcmPinNumber = (int)bcmPinNumber;

            WiringPiPinNumber = BcmToWiringPiPinNumber(bcmPinNumber);
            PhysicalPinNumber = Definitions.BcmToPhysicalPinNumber(SystemInfo.GetBoardRevision(), bcmPinNumber);
            Header = (BcmPinNumber >= 28 && BcmPinNumber <= 31) ? GpioHeader.P5 : GpioHeader.P1;
        }

        #endregion

        #region Pin Properties

        /// <inheritdoc />
        public BcmPin BcmPin { get; }

        /// <inheritdoc />
        public int BcmPinNumber { get; }

        /// <inheritdoc />
        public int PhysicalPinNumber { get; }

        /// <summary>
        /// Gets the WiringPi Pin number.
        /// </summary>
        public WiringPiPin WiringPiPinNumber { get; }

        /// <inheritdoc />
        public GpioHeader Header { get; }

        /// <summary>
        /// Gets the friendly name of the pin.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the hardware mode capabilities of this pin.
        /// </summary>
        public PinCapability Capabilities { get; private set; }

        /// <inheritdoc />
        public bool Value
        {
            get => Read();
            set => Write(value);
        }

        #endregion

        #region Hardware-Specific Properties

        /// <inheritdoc />
        /// <exception cref="T:System.NotSupportedException">Thrown when a pin does not support the given operation mode.</exception>
        public GpioPinDriveMode PinMode
        {
            get => _pinMode;

            set
            {
                lock (_syncLock)
                {
                    var mode = value;
                    if ((mode == GpioPinDriveMode.GpioClock && !HasCapability(PinCapability.GPCLK)) ||
                        (mode == GpioPinDriveMode.PwmOutput && !HasCapability(PinCapability.PWM)) ||
                        (mode == GpioPinDriveMode.Input && !HasCapability(PinCapability.GP)) ||
                        (mode == GpioPinDriveMode.Output && !HasCapability(PinCapability.GP)))
                    {
                        throw new NotSupportedException(
                            $"Pin {BcmPinNumber} '{Name}' does not support mode '{mode}'. Pin capabilities are limited to: {Capabilities}");
                    }

                    WiringPi.PinMode(BcmPinNumber, (int)mode);
                    _pinMode = mode;
                }
            }
        }

        /// <summary>
        /// Gets the interrupt callback. Returns null if no interrupt
        /// has been registered.
        /// </summary>
        private Action InterruptCallback { get; set; } = null;

        /// <summary>
        /// Calls the registered Interrupt callback routine when there is one registered
        /// </summary>
        private void CallRegisteredInterruptCallback() => InterruptCallback?.Invoke();

        /// <summary>
        /// Gets the interrupt edge detection mode.
        /// </summary>
        public EdgeDetection InterruptEdgeDetection { get; private set; }

        /// <summary>
        /// Determines whether the specified capability has capability.
        /// </summary>
        /// <param name="capability">The capability.</param>
        /// <returns>
        ///   <c>true</c> if the specified capability has capability; otherwise, <c>false</c>.
        /// </returns>
        public bool HasCapability(PinCapability capability) =>
            (Capabilities & capability) == capability;

        #endregion

        #region Hardware PWM Members

        /// <inheritdoc />
        public GpioPinResistorPullMode InputPullMode
        {
            get => PinMode == GpioPinDriveMode.Input ? _resistorPullMode : GpioPinResistorPullMode.Off;

            set
            {
                lock (_syncLock)
                {
                    if (PinMode != GpioPinDriveMode.Input)
                    {
                        _resistorPullMode = GpioPinResistorPullMode.Off;
                        throw new InvalidOperationException(
                            $"Unable to set the {nameof(InputPullMode)} for pin {BcmPinNumber} because operating mode is {PinMode}."
                            + $" Setting the {nameof(InputPullMode)} is only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Input}");
                    }

                    WiringPi.PullUpDnControl(BcmPinNumber, (int)value);
                    _resistorPullMode = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the PWM register.
        /// </summary>
        /// <value>
        /// The PWM register.
        /// </value>
        public int PwmRegister
        {
            get => _pwmRegister;

            set
            {
                lock (_syncLock)
                {
                    if (!HasCapability(PinCapability.PWM))
                    {
                        _pwmRegister = 0;

                        throw new NotSupportedException(
                            $"Pin {BcmPinNumber} '{Name}' does not support mode '{GpioPinDriveMode.PwmOutput}'. Pin capabilities are limited to: {Capabilities}");
                    }

                    WiringPi.PwmWrite(BcmPinNumber, value);
                    _pwmRegister = value;
                }
            }
        }

        /// <summary>
        /// The PWM generator can run in 2 modes – “balanced” and “mark:space”. The mark:space mode is traditional,
        /// however the default mode in the Pi is “balanced”.
        /// </summary>
        /// <value>
        /// The PWM mode.
        /// </value>
        /// <exception cref="InvalidOperationException">When pin mode is not set a Pwn output.</exception>
        public PwmMode PwmMode
        {
            get => PinMode == GpioPinDriveMode.PwmOutput ? _pwmMode : PwmMode.Balanced;

            set
            {
                lock (_syncLock)
                {
                    if (!HasCapability(PinCapability.PWM))
                    {
                        _pwmMode = PwmMode.Balanced;

                        throw new NotSupportedException(
                            $"Pin {BcmPinNumber} '{Name}' does not support mode '{GpioPinDriveMode.PwmOutput}'. Pin capabilities are limited to: {Capabilities}");
                    }

                    WiringPi.PwmSetMode((int)value);
                    _pwmMode = value;
                }
            }
        }

        /// <summary>
        /// This sets the range register in the PWM generator. The default is 1024.
        /// </summary>
        /// <value>
        /// The PWM range.
        /// </value>
        /// <exception cref="InvalidOperationException">When pin mode is not set to PWM output.</exception>
        public uint PwmRange
        {
            get => PinMode == GpioPinDriveMode.PwmOutput ? _pwmRange : 0;

            set
            {
                lock (_syncLock)
                {
                    if (!HasCapability(PinCapability.PWM))
                    {
                        _pwmRange = 1024;

                        throw new NotSupportedException(
                            $"Pin {BcmPinNumber} '{Name}' does not support mode '{GpioPinDriveMode.PwmOutput}'. Pin capabilities are limited to: {Capabilities}");
                    }

                    WiringPi.PwmSetRange(value);
                    _pwmRange = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the PWM clock divisor.
        /// </summary>
        /// <value>
        /// The PWM clock divisor.
        /// </value>
        /// <exception cref="InvalidOperationException">When pin mode is not set to PWM output.</exception>
        public int PwmClockDivisor
        {
            get => PinMode == GpioPinDriveMode.PwmOutput ? _pwmClockDivisor : 0;

            set
            {
                lock (_syncLock)
                {
                    if (!HasCapability(PinCapability.PWM))
                    {
                        _pwmClockDivisor = 1;

                        throw new NotSupportedException(
                            $"Pin {BcmPinNumber} '{Name}' does not support mode '{GpioPinDriveMode.PwmOutput}'. Pin capabilities are limited to: {Capabilities}");
                    }

                    WiringPi.PwmSetClock(value);
                    _pwmClockDivisor = value;
                }
            }
        }

        #endregion

        #region Software Tone Members

        /// <summary>
        /// Gets a value indicating whether this instance is in software based tone generator mode.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in soft tone mode; otherwise, <c>false</c>.
        /// </value>
        public bool IsInSoftToneMode => _softToneFrequency >= 0;

        /// <summary>
        /// Gets or sets the soft tone frequency. 0 to 5000 Hz is typical.
        /// </summary>
        /// <value>
        /// The soft tone frequency.
        /// </value>
        /// <exception cref="InvalidOperationException">When soft tones cannot be initialized on the pin.</exception>
        public int SoftToneFrequency
        {
            get => _softToneFrequency;

            set
            {
                lock (_syncLock)
                {
                    if (IsInSoftToneMode == false)
                    {
                        var setupResult = WiringPi.SoftToneCreate(BcmPinNumber);
                        if (setupResult != 0)
                        {
                            throw new InvalidOperationException(
                                $"Unable to initialize soft tone on pin {BcmPinNumber}. Error Code: {setupResult}");
                        }
                    }

                    WiringPi.SoftToneWrite(BcmPinNumber, value);
                    _softToneFrequency = value;
                }
            }
        }

        #endregion

        #region Software PWM Members

        /// <summary>
        /// Gets a value indicating whether this pin is in software based PWM mode.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in soft PWM mode; otherwise, <c>false</c>.
        /// </value>
        public bool IsInSoftPwmMode => _softPwmValue >= 0;

        /// <summary>
        /// Gets or sets the software PWM value on the pin.
        /// </summary>
        /// <value>
        /// The soft PWM value.
        /// </value>
        /// <exception cref="InvalidOperationException">StartSoftPwm.</exception>
        public int SoftPwmValue
        {
            get => _softPwmValue;

            set
            {
                lock (_syncLock)
                {
                    if (IsInSoftPwmMode && value >= 0)
                    {
                        WiringPi.SoftPwmWrite(BcmPinNumber, value);
                        _softPwmValue = value;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Software PWM requires a call to {nameof(StartSoftPwm)}.");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the software PWM range used upon starting the PWM.
        /// </summary>
        public int SoftPwmRange { get; private set; } = -1;

        /// <summary>
        /// Starts the software based PWM on this pin.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="range">The range.</param>
        /// <exception cref="NotSupportedException">When the pin does not suppoert PWM.</exception>
        /// <exception cref="InvalidOperationException">StartSoftPwm
        /// or.</exception>
        public void StartSoftPwm(int value, int range)
        {
            lock (_syncLock)
            {
                if (!HasCapability(PinCapability.GP))
                    throw new NotSupportedException($"Pin {BcmPinNumber} does not support software PWM");

                if (IsInSoftPwmMode)
                    throw new InvalidOperationException($"{nameof(StartSoftPwm)} has already been called.");

                var startResult = WiringPi.SoftPwmCreate(BcmPinNumber, value, range);

                if (startResult == 0)
                {
                    _softPwmValue = value;
                    SoftPwmRange = range;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Could not start software based PWM on pin {BcmPinNumber}. Error code: {startResult}");
                }
            }
        }

        #endregion

        #region Output Mode (Write) Members

        /// <inheritdoc />
        public void Write(GpioPinValue value)
        {
            lock (_syncLock)
            {
                if (PinMode != GpioPinDriveMode.Output)
                {
                    throw new InvalidOperationException(
                        $"Unable to write to pin {BcmPinNumber} because operating mode is {PinMode}."
                        + $" Writes are only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Output}");
                }

                WiringPi.DigitalWrite(BcmPinNumber, (int)value);
            }
        }

        /// <summary>
        /// Writes the value asynchronously.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The awaitable task.</returns>
        public Task WriteAsync(GpioPinValue value) => Task.Run(() => { Write(value); });

        /// <summary>
        /// Writes the specified bit value.
        /// This method performs a digital write.
        /// </summary>
        /// <param name="value">if set to <c>true</c> [value].</param>
        public void Write(bool value)
            => Write(value ? GpioPinValue.High : GpioPinValue.Low);

        /// <summary>
        /// Writes the specified bit value.
        /// This method performs a digital write.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The awaitable task.
        /// </returns>
        public Task WriteAsync(bool value) => Task.Run(() => { Write(value); });

        /// <summary>
        /// Writes the specified value. 0 for low, any other value for high
        /// This method performs a digital write.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Write(int value) => Write(value != 0 ? GpioPinValue.High : GpioPinValue.Low);

        /// <summary>
        /// Writes the specified value. 0 for low, any other value for high
        /// This method performs a digital write.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The awaitable task.</returns>
        public Task WriteAsync(int value) => Task.Run(() => { Write(value); });

        /// <summary>
        /// Writes the specified value as an analog level.
        /// You will need to register additional analog modules to enable this function for devices such as the Gertboard.
        /// </summary>
        /// <param name="value">The value.</param>
        public void WriteLevel(int value)
        {
            lock (_syncLock)
            {
                if (PinMode != GpioPinDriveMode.Output)
                {
                    throw new InvalidOperationException(
                        $"Unable to write to pin {BcmPinNumber} because operating mode is {PinMode}."
                        + $" Writes are only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Output}");
                }

                WiringPi.AnalogWrite(BcmPinNumber, value);
            }
        }

        /// <summary>
        /// Writes the specified value as an analog level.
        /// You will need to register additional analog modules to enable this function for devices such as the Gertboard.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The awaitable task.</returns>
        public Task WriteLevelAsync(int value) => Task.Run(() => { WriteLevel(value); });

        #endregion

        #region Input Mode (Read) Members

        /// <summary>
        /// Wait for specific pin status.
        /// </summary>
        /// <param name="status">status to check.</param>
        /// <param name="timeOutMillisecond">timeout to reach status.</param>
        /// <returns>true/false.</returns>
        public bool WaitForValue(GpioPinValue status, int timeOutMillisecond)
        {
            if (PinMode != GpioPinDriveMode.Input)
            {
                throw new InvalidOperationException(
                    $"Unable to read from pin {BcmPinNumber} because operating mode is {PinMode}."
                    + $" Reads are only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Input}");
            }

            var hrt = new HighResolutionTimer();
            hrt.Start();
            do
            {
                if (ReadValue() == status)
                    return true;
            }
            while (hrt.ElapsedMilliseconds <= timeOutMillisecond);

            return false;
        }

        /// <summary>
        /// Reads the digital value on the pin as a boolean value.
        /// </summary>
        /// <returns>The state of the pin.</returns>
        public bool Read()
        {
            lock (_syncLock)
            {
                if (PinMode != GpioPinDriveMode.Input && PinMode != GpioPinDriveMode.Output)
                {
                    throw new InvalidOperationException(
                        $"Unable to read from pin {BcmPinNumber} because operating mode is {PinMode}."
                        + $" Reads are only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Input} or {GpioPinDriveMode.Output}");
                }

                return WiringPi.DigitalRead(BcmPinNumber) != 0;
            }
        }

        /// <summary>
        /// Reads the digital value on the pin as a boolean value.
        /// </summary>
        /// <returns>The state of the pin.</returns>
        public Task<bool> ReadAsync() => Task.Run(Read);

        /// <summary>
        /// Reads the digital value on the pin as a High or Low value.
        /// </summary>
        /// <returns>The state of the pin.</returns>
        public GpioPinValue ReadValue()
            => Read() ? GpioPinValue.High : GpioPinValue.Low;

        /// <summary>
        /// Reads the digital value on the pin as a High or Low value.
        /// </summary>
        /// <returns>The state of the pin.</returns>
        public Task<GpioPinValue> ReadValueAsync() => Task.Run(ReadValue);

        /// <summary>
        /// Reads the analog value on the pin.
        /// This returns the value read on the supplied analog input pin. You will need to register
        /// additional analog modules to enable this function for devices such as the Gertboard,
        /// quick2Wire analog board, etc.
        /// </summary>
        /// <returns>The analog level.</returns>
        /// <exception cref="InvalidOperationException">When the pin mode is not configured as an input.</exception>
        public int ReadLevel()
        {
            lock (_syncLock)
            {
                if (PinMode != GpioPinDriveMode.Input)
                {
                    throw new InvalidOperationException(
                        $"Unable to read from pin {BcmPinNumber} because operating mode is {PinMode}."
                        + $" Reads are only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Input}");
                }

                return WiringPi.AnalogRead(BcmPinNumber);
            }
        }

        /// <summary>
        /// Reads the analog value on the pin.
        /// This returns the value read on the supplied analog input pin. You will need to register
        /// additional analog modules to enable this function for devices such as the Gertboard,
        /// quick2Wire analog board, etc.
        /// </summary>
        /// <returns>The analog level.</returns>
        public Task<int> ReadLevelAsync() => Task.Run(ReadLevel);

        #endregion

        #region Interrupts

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">callback.</exception>
        /// <exception cref="InvalidOperationException">callback.</exception>
        public void RegisterInterruptCallback(EdgeDetection edgeDetection, Action callback)
        {
            if (InterruptCallback != null)
                throw new InvalidOperationException("Interrupt already registered.");
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (PinMode != GpioPinDriveMode.Input)
            {
                throw new InvalidOperationException(
                    $"Unable to {nameof(RegisterInterruptCallback)} for pin {BcmPinNumber} because operating mode is {PinMode}."
                    + $" Calling {nameof(RegisterInterruptCallback)} is only allowed if {nameof(PinMode)} is set to {GpioPinDriveMode.Input}");
            }

            lock (_syncLock)
            {
                var isrCallback = new InterruptServiceRoutineCallback(CallRegisteredInterruptCallback);
                var registerResult = WiringPi.WiringPiISR(BcmPinNumber, GetWiringPiEdgeDetection(edgeDetection), isrCallback);
                if (registerResult == 0)
                {
                    InterruptEdgeDetection = edgeDetection;
                    InterruptCallback = callback;
                }
                else
                {
                    HardwareException.Throw(nameof(GpioPin), nameof(RegisterInterruptCallback));
                }
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">callback.</exception>
        /// <exception cref="InvalidOperationException">callback.</exception>
        public void RemoveInterruptCallback(EdgeDetection edgeDetection, Action callback)
        {
            if (InterruptCallback == null)
                throw new InvalidOperationException("No Interrupt currently Registered.");
            if (callback != InterruptCallback)
                throw new InvalidOperationException("Currently a differend callabck is registered.");

            lock (_syncLock)
            {
                InterruptCallback = null;
            }
        }

        /// <inheritdoc />
        public void RegisterInterruptCallback(EdgeDetection edgeDetection, Action<int, int, uint> callback) =>
            throw new NotSupportedException("WiringPi does only support a simple interrupt callback that has no parameters.");

        /// <inheritdoc />
        public void RemoveInterruptCallback(EdgeDetection edgeDetection, Action<int, int, uint> callback) =>
            throw new NotSupportedException("WiringPi does only support a simple interrupt callback that has no parameters.");

        internal static WiringPiPin BcmToWiringPiPinNumber(BcmPin pin) =>
            (WiringPiPin)GpioToWiringPi[(int)pin];

        private static int GetWiringPiEdgeDetection(EdgeDetection edgeDetection) =>
            GpioController.WiringPiEdgeDetectionMapping[edgeDetection];

        #endregion
    }
}
