## DSPi CommandLine Client and Server
#### version 1
MZachmann

The DSPiCliRemote project contains a server application: DSPiCliServer that is a tiny web page and command line interface (CLI) server intended to work with the [WeebLabs DSPi project](https://github.com/WeebLabs/DSPi).

DSPiCliServer contains a quick gui via web browser, a CLI, and a REST interface. The CLI works with the DSPi Console application to run the console remotely
on a Windows machine while DSPiCliServer is running on whatever is connected to the DSPi Pico USB port.

#### Note: this requires .NET 8.

### What is it?

The **Server** is a tiny (less than 1MB currently) console application that runs in Windows,
Mac, and Linux (Ubuntu and Raspbian are tested).

Install/run it on the computer driving the DSPi Pico USB port. The current version takes very few resources. Top on a **Pi Zero 2** shows it
using about 1% of the CPU. It uses about 25MB of ram.

<img src="https://github.com/MZachmann/DSPiCliRemote/raw/main/Images/Gui001.png" alt="DSPiCliServer gui1" width="400" >
<img src="https://github.com/MZachmann/DSPiCliRemote/raw/main/Images/Gui002.png" alt="DSPiCliServer gui2" width="350" >

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
* copy the two DSPi projects (DSPiConsole.Usb and DSPiConsole.Core) from the github DSPi-Console-Windows repository into the solution folder
  * change the target of the two projects to .NET 8 instead of the framework 8
  * replace the Color definition in DSPiConsole.Core:Channel.cs to use Windows.System.Color instead of Windows.IO.Color
  * Make DSPiConsole.Usb:ControlTransferIn/Out public methods


## non-Windows Operating System Installations
All of these use the labeled zips and need to make the program executable.

### Mac OS X
Start by installing .NET 8 from the web.

1. Get the DSPiCliRemote ZIP file from the repository Releases
2. Run chmod +x on the application file (DSPiCliServer) and if you want to use it, also on the PipeIt application file in the subfolder.
3. Run the application file (DSPiCliServer)

After you try to run the app the first time, unblock it in the Privacy tab of the Security & Privacy settings and allow it to run/communicate.

### Ubuntu & Raspbian
1. I installed .NET 8 by going to the Microsoft site and following the instructions.
    https://dotnet.microsoft.com/en-us/download/dotnet/8.0

    It replies with this page
    https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.420-linux-arm64-binaries

    And displays this script to install that version of it.
    
    Note that if you want to run DSPiCliServer as root (during startup) then the .bashrc path export lines should
    also be in the root startup script but replacing $HOME with /home/myusername.

```bash
    mkdir -p $HOME/dotnet && tar zxf dotnet-sdk-8.0.420-linux-arm64.tar.gz -C $HOME/dotnet
    echo "export DOTNET_ROOT=\$HOME/dotnet" >>~/.bashrc
    echo "export PATH=\$PATH:\$HOME/dotnet:\$HOME/dotnet/tools" >>~/.bashrc
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

### Raspbian Autostart
If you want to autostart a Pi with the cliserver. Assuming you installed .NET in your home folder as described above...

1. create a bash script in your home folder to point to the .NET path. I place the app folder on my desktop. It's probably
better in your home folder. Anyway, 
```bash
nano dspicli.sh
chmod +x dspicli.sh
```

``````
#!/bin/bash
export DOTNET_ROOT=/home/myusername/dotnet
export PATH=$PATH:/home/myusername/dotnet:/home/myusername/dotnet/tools
cd /home/myusername/Desktop/DSPiCliRemote-LinuxArm64
./DSPiCliServer
``````

2. put a new service file into /usr/lib/systemd/system :
```bash
sudo nano /usr/lib/systemd/system/DSPiCliRemote.service
```

``````
Description=DSPiCliServer
After=multi-user.target

[Service]
Type=idle
ExecStart=/home/myusername/dspicli.sh

[Install]
WantedBy=multi-user.target
``````
Then enable and start the service using
```bash
sudo systemctl enable DSPiCliRemote.service
sudo systemctl start DSPiCliRemote.service
```


### Changelog

6/16/26
1. Update to the latest console code to ensure compatability
2. Remove bottleneck with .usb to boost performance

6/9/26
1. Add auto-shown sliders for loudness and level setting.
2. Add an optional slider for User volume.
3. Enable hiding Master volume slider.

6/2/26
1. Update to the latest console code to ensure compatability

05/26/26
1. Add controls to set loudness amount and leveling compression when the 
associated states are on.
2. Have the state buttons change to green when on
3. Add more help to the CLI display and add the new commands to the CLI and api
4. Update the image
