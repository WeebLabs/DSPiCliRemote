## DSPi CommandLine Client and Server
MZachmann

This is a tiny web page provider intended to work with the [WeebLabs DSPi project](https://github.com/WeebLabs/DSPi) that provides a quick gui via web browser.

### To run it:
* download the latest release from the [releases page](https://github.com/WeebLabs/DSPiCommandLineClient/releases)
* install .NET 10 if you haven't already
* unzip the archive into a folder
* run the server and it should start listening then sit there
* run a browser and go to http://yourip:yourport

### To build it:
* open the solution in Visual Studio or JetBrains Rider
* copy the two DSPi project (DSPiConsole.Usb and DSPiConsole.Core) into the solution folder

### What is it?

The **Server** is a small (about 5MB currently) console application that runs in Windows, 
Mac, and Linux. Install/run it on the computer driving the DSPi Pico USB port. It takes very few resources.

Web View
![web](./Images/Gui001.png)

The Server listens to two network ports (your choice which) and 
* on one port it has a simple 
**web server that provies a one page GUI** to adjust volume, loudness and presets
* on the second port it allows **CLI clients** to attach for freeform text commanding.

The _optional_ **Client** is a smallish .NET Core / Amazonia application that runs in Windows, 
Mac, and Linux. It should run on any desktop (Windows, Mac, Linux). It has a small 
user interface that lets you send text queries and commands to the DSPi Pico and get text results. 
It also has a secondary Window with a display like the web page.