# DesktopImage
Show an image overlay on the Windows Desktop, customizable for position, scale, opacity, tablet rotation.

A small executable that places a semi-transparent watermark/image overlay on windows desktops. 
Use to display a reminder, logo, correct a white spot screen blemish, etc.
Controlled by config file DesktopImage.ini in the same dir as the executable, which will be created automatically if not present. Settings for position, scale, opacity, tablet rotation. Image can be static or animated
Image shows while exe left running, with small memory use, and does not affect use of application windows underneath. 
To automatically show the image when user logs on to the computer, put a shortcut to the executable in folder C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp
Free to use & modify: targets .NET 4, solution/project files for Visual Studio. 
Uses some code from customdesktoplogo.wikidot.com  (Custom Desktop Logo, Eric Wong 2008, licensed under GNU GPLv3). 
