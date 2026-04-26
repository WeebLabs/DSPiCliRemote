## DSPi CommandLine Client and Server
MZachmann

This is a third party application intended to work with the [WeebLabs DSPi project](https://github.com/WeebLabs/DSPi).

The **Server** is a small (about 5MB currently) console application that runs in Windows, 
Mac, and Linux. Install it on the computer driving the DSPi Pico USB port. It takes very few resources.

The Server listens to two network ports (your choice which) and 
* on one port it has a simple 
**web server that provies a one page GUI** to adjust volume, loudness and presets
* on the second port it allows **CLI clients** to attach for freeform text commanding.

The _optional_ **Client** is a smallish .NET Core / Amazonia application that runs in Windows, 
Mac, and Linux. It should run on any desktop (Windows, Mac, Linux). It has a small 
user interface that lets you send text queries and commands to the DSPi Pico and get text results. 
It also has a secondary Window with a display like the web page.