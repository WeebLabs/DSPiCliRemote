## DSPi CommandLine Client and Server
MZachmann

This is a tiny web page provider intended to work with the [WeebLabs DSPi project](https://github.com/WeebLabs/DSPi) that provides a quick 
gui via web browser as well as a REST interface.

#### Note: this now requires .NET 8.

### What is it?

The **Server** is a small (about 5MB currently) console application that runs in Windows,
Mac, and Linux (Ubuntu and Raspbian are tested).

Install/run it on the computer driving the DSPi Pico USB port. It takes very few resources. Top on a PI5 shows it
using less than 1% of the CPU.

![web](./Images/Gui001.png)

* The **USB** button lets you select between USB and SPDIF input sources.

* The **CLI** button switches to the CLI mode to let you type commands, such as set_str. The "help" command lists all commands.

* The **Script** button runs a script of your choice. While running, the button shows On. 
Click it while On to kill the running process. By default it runs a user-created Optical.sh (or .bat or .ps1) 
if found in the app folder.
If none of those are found the default is set to run PipeIt (pipe audio input to audio output) for Mac OSX. See set_str.

The REST interface lives at http://your_ip_address:your_port/api/ where you can send simple commands such
as
```bash
http://1.2.3.4:8082/api/get_str 
or
http://myweb.com:8082/api/get_str
```
 and if the command has an argument just put a ? as delimiter. Such as
```bash
http://1.2.3.4:8082/api/set_str?my_new_string
```

The Server listens to two network ports (your choice which) and
* the first port has a simple
  **web server that provides a one page GUI** to adjust volume, loudness and presets
* the second port allows **CLI clients** to attach for freeform text commanding.

### To prepare to run it:
* install .NET 8 if you haven't already (see below)
* download the latest release (zip file) from the [releases page](https://github.com/MZachmann/DSPiCliRemote/releases)
* * unzip that into a folder

### To run it:
* in that folder, run the server 
* it should print a few lines about which ports you've picked 
then it starts listening and just sits there
* run a browser and go to http://yourip:yourport to see the web page

### To build it: (not required)
* open the solution in Visual Studio or JetBrains Rider
* copy the two DSPi project (DSPiConsole.Usb and DSPiConsole.Core) into the solution folder
* change the target of the two projects to .NET 8 instead of the framework 8
* one file (Channel.cs) takes a minor change to include Windows.System .Color instead of Windows.IO .Color

## non-Windows Operating System Installations
All of these use the labeled zips and (except Mac) need to make the program executable.

### Mac OS X
Start by installing .NET 8 from the web.

1. Get the DSPiCliRemote ZIP file from the repository Releases
2. Run chmod +x on the application file (DSPiCliRemote) and if you want to use it, also on the PipeIt application file in the subfolder.
3. Run the application file (DSPiCliRemote)

After you try to run the app, unblock it in the Privacy tab of the Security & Privacy settings and allow it to run/communicate.

### Ubuntu & Raspbian
1. I installed .NET 8 by going to the Microsoft site and following the instructions.
    https://dotnet.microsoft.com/en-us/download/dotnet/8.0

    It replies with this page
    https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.420-linux-arm64-binaries

    And displays this script to install that version of it.
```bash
    mkdir -p $HOME/dotnet && tar zxf dotnet-sdk-8.0.420-linux-arm64.tar.gz -C $HOME/dotnet
    echo "export DOTNET_ROOT=\$HOME/dotnet" >>.bashrc
    echo "export PATH=\$PATH:\$HOME/dotnet:\$HOME/dotnet/tools" >>.bashrc
    source ~/.bashrc
```
2. They both require installation of libusb-1.0 via
```bash
sudo apt install libusb-1.0-0-dev 
```
3. The USB ports in Linux start read-only so you have to add permission to the USB port hardware.
The simplest approach is to run this bash script below which will give your user read-write
access to all USB devices.
```bash
# Create a group called usbwriters
sudo groupadd usbwriters
# Add you to the group
sudo usermod -a -G usbwriters $USER
# Allow access to the USB port by adding a rule granting the group access to the subsystem
echo 'SUBSYSTEM=="usb", MODE="0660", GROUP="usbwriters"' | sudo tee /etc/udev/rules.d/99-usbwriters.rules
# Reboot
echo "Please reboot"
```
4. Run chmod +x on the application file to allow executing it

That should be the prerequisites.

