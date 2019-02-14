[![Build status](https://ci.appveyor.com/api/projects/status/396f8xpnuj2qq4f1/branch/master?svg=true)](https://ci.appveyor.com/project/geoperez/wiringpi-dotnet/branch/master)

# <img src="https://raw.githubusercontent.com/unosquare/wiringpi-dotnet/master/logos/raspberryio-logo-32.png" alt="wiringpi-dotnet" style="width:16px; height:16px" /> wiringpi-dotnet

Provides complete managed access to the popular wiringpi C library

The default low-level provider is the wonderful ```WiringPi``` library available [here](http://wiringpi.com/). You do not need to install this library yourself. The ```Unosquare.WiringPi``` assembly will automatically extract the compiled binary of the library in the same path as the entry assembly.

## Installation

Install basic Raspberry.IO package:
[![NuGet version](https://badge.fury.io/nu/Unosquare.Raspberry.IO.svg)](https://badge.fury.io/nu/Unosquare.Raspberry.IO)

```
PM> Install-Package Unosquare.Raspberry.IO
```

Install WiringPi implementation:
[![NuGet version](https://badge.fury.io/nu/Unosquare.wiringpi.svg)](https://badge.fury.io/nu/Unosquare.wiringpi)

```
PM> Install-Package Unosquare.WiringPi
```

## Obtaining Board and System Information
```RaspberryIO``` contains useful utilities to obtain information about the board it is running on. You can simply call the ```Pi.Info.ToString()``` method to obtain a dump of all system properties as a single ```string```, or you can use the individual properties such as Installed RAM, Processor Count, Raspberry Pi Version, Serial Number, etc. There's not a lot more to this.

Please note ```Pi.Info``` depends on ```Wiring Pi```, and the ```/proc/cpuinfo``` and ```/proc/meminfo``` files.

## Using the GPIO Pins
Pin reference for the B plus (B+) - Header P1

| BCM |  Name    | Mode | V   | L      | R      | V   | Mode | Name    |  BCM |
| --- | -------- | ---- | --- | ------ | ------ | --- | ---- | ------- | ---- |
|     |     3.3v |      |     | **01** | **02** |     |      | 5v      |      |
|   2 |    SDA.1 | ALT0 | 1   | **03** | **04** |     |      | 5V      |      |
|   3 |    SCL.1 | ALT0 | 1   | **05** | **06** |     |      | GND     |      |
|   4 |  GPIO. 7 |   IN | 1   | **07** | **08** | 1   | ALT0 | TxD     |  14  |
|     |      GND |      |     | **09** | **10** | 1   | ALT0 | RxD     |  15  |
|  17 |  GPIO. 0 |   IN | 0   | **11** | **12** | 0   | IN   | GPIO. 1 |  18  |
|  27 |  GPIO. 2 |   IN | 0   | **13** | **14** |     |      | GND     |      |
|  22 |  GPIO. 3 |   IN | 0   | **15** | **16** | 0   | IN   | GPIO. 4 |  23  |
|     |     3.3v |      |     | **17** | **18** | 0   | IN   | GPIO. 5 |  24  |
|  10 |     MOSI |   IN | 0   | **19** | **20** |     |      | GND     |      |
|   9 |     MISO |   IN | 0   | **21** | **22** | 0   | IN   | GPIO. 6 |  25  |
|  11 |     SCLK |   IN | 0   | **23** | **24** | 1   | IN   | CE0     |  8   |
|     |      GND |      |     | **25** | **26** | 1   | IN   | CE1     |  7   |
|   0 |    SDA.0 |   IN | 1   | **27** | **28** | 1   | IN   | SCL.0   |  1   |
|   5 |  GPIO.21 |   IN | 1   | **29** | **30** |     |      | GND     |      |
|   6 |  GPIO.22 |   IN | 1   | **31** | **32** | 0   | IN   | GPIO.26 |  12  |
|  13 |  GPIO.23 |   IN | 0   | **33** | **34** |     |      | GND     |      |
|  19 |  GPIO.24 |   IN | 0   | **35** | **36** | 0   | IN   | GPIO.27 |  16  |
|  26 |  GPIO.25 |   IN | 0   | **37** | **38** | 0   | IN   | GPIO.28 |  20  |
|     |      GND |      |     | **39** | **40** | 0   | IN   | GPIO.29 |  21  |

The above diagram shows the pins of GPIO Header P1. There is an additional GPIO header on the Pi called P5. [More info available here](http://www.raspberrypi-spy.co.uk/2012/09/raspberry-pi-p5-header/)

In order to access the pins, use ```Pi.Gpio```. The pins can have multiple behaviors and fortunately ```Pi.Gpio``` can be iterated, addressed by index, addressed by BCM pin number and provides the pins as publicly accessible properties.

Here is an example of addressing the pins in all the various ways:

```csharp
public static void TestLedBlinking()
{
    // Get a reference to the pin you need to use.
    // All 3 methods below are exactly equivalent
    var blinkingPin = Pi.Gpio[17];
    blinkingPin = Pi.Gpio[BcmPin.Gpio17];
    blinkingPin = Pi.Gpio.Pin17;

    // Configure the pin as an output
    blinkingPin.PinMode = GpioPinDriveMode.Output;

    // perform writes to the pin by toggling the isOn variable
    var isOn = false;
    for (var i = 0; i < 20; i++)
    {
        isOn = !isOn;
        blinkingPin.Write(isOn);
        System.Threading.Thread.Sleep(500);
    }
}
```

## Related Projects and Nugets
| Name | Author | Description |
| ---- | ------ | ----------- |
| [RaspberryIO](https://github.com/unosquare/raspberryio) | [Unosquare](https://github.com/unosquare) | The Raspberry Pi's IO Functionality in an easy-to-use API for .NET (Mono/.NET Core). |
| [PiGpio.net](https://github.com/unosquare/pigpio-dotnet) | [Unosquare](https://github.com/unosquare) | Provides complete managed access to the popular pigpio C library |
| [Raspberry Abstractions](https://www.nuget.org/packages/Unosquare.Raspberry.Abstractions) | [Unosquare](https://www.nuget.org/profiles/Unosquare) | Allows you to implement your own provider for RaspberryIO. |
| [Raspberry# IO](https://github.com/raspberry-sharp/raspberry-sharp-io) | [raspberry-sharp](https://github.com/raspberry-sharp) | Raspberry# IO is a .NET/Mono IO Library for Raspberry Pi. This project is an initiative of the [Raspberry#](http://www.raspberry-sharp.org/) Community. |
| [WiringPi.Net](https://github.com/danriches/WiringPi.Net) | [Daniel Riches](https://github.com/danriches) | A simple C# wrapper for Gordon's WiringPi library. |
| [PiSharp](https://github.com/andycb/PiSharp) |[Andy Bradford](https://github.com/andycb) | Pi# is a library to expose the GPIO functionality of the Raspberry Pi computer to the C# and Visual Basic.Net languages |
