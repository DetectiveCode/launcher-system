This scuffed launcher project, written in a day will let you patch and update your application as well as scan client files to make sure it has not been modified. It offers you the freedom of customisation without having to modify the code.

# Section 1: Client Side
On the client side you will have the following files:
- reslauncher
- 7za.dll
- Launcher.exe
- LauncherUpdater.exe
- SevenZipSharp.dll

## DLL Files
The dll files listed below are required for the launcher and launcher updater to be able to operate.
- 7za.dll
- SevenZipSharp.dll

## Binary Files
The “Launcher.exe” file is your launcher. You use this to patch and start your application. The launcher updater serves as a tool to update the launcher in the future if required.

## Folder
In the reslauncher folder the “config.txt” file and all patch files are stored. Normally you would not be required to touch anything in this folder. In the config file, you will need to specify the url to the launcher config which is on your web server.
```
launcherconfig = http://yourdomain.com/launcher/launcherconfig.txt
clientversion = 0
launcherversion = 0
```
In the case where you are required to update your launcher server settings. Please refer to the “config.txt” file provided to your in or simply make a new text file with the above format and name. Finally copy and paste or move your new config to the reslauncher folder and replace the existing one.

## Resetting Patch Version
To allow for players to patch again there is a simple functionality implemented. Right click the play (or start) button twice and accept the dialogue.

# Section 2: Server Side
On the server side you will have the following files:
- launcherpatches
- patches
- 1.htaccess
- filecheck.txt
- launcherconfig.txt
- launcherpatchfile.txt
- patchfile.txt
- patchnote.txt

## Folder
The folders “launcherpatches” and “patches” can be used to store your patch files. You don’t necessarily need to use these folders.

## Access File
The “1.htaccess” file is used to prevent users from directly accessing the file space by visiting the url “http://yourdomain.com/launcher”. 

## File Check
The file check system is a feature in the launcher which scans all of the server specified files.
The example below demonstrates how to use the file check system. You first specify the path, from the root directory or the client and the file name including the extension. Then you specify the MD5 checksum of that file.
```
/path/to/file.txt = 3D8E577BDDB17DB339EAE0B3D9BCF180
```
After the player clicks play, the launcher will read the specified files, calculate the MD5 checksum of the locally stored file and compare it to the server-side version.
You can calculate the MD5 of a file by using HxD (Analysis>Checksums) or windows command prompt. Please research this on Google.

## Patch Notes
In the patch notes file, you are able to write anything. When the launcher is started, it will then look check the server for the latest patch notes and display them in the launcher. If you want to not have patch notes, you can leave this file blank.
```
Patch 1.0 - DD.MM.YYYY

Example patch notes... For this patch the main focus was improving this and that, and that other thing as well.. Casual stuff.

- Update 1
- Update 2
- Update 3
- Update 4
- Update 5
- Update 6
- Update 7
- Update 8
- Update 9
- Update 10

Thanks for everyone who reported bugs, keep up the good work, whatever, feel appreciated guys.

Staff Team
```

## Client and Launcher Patch Files
There is one file called “patchfile.txt” which is used for client patches and there is one file called “launcherpatchfile.txt” which is used for launcher patches.
The structure of the file is simple.
```
1#http://examplelink.com/patch1.7z
```
The version number which the patch will be, followed by a “#” and then the link to download the file. Each patch should increment the version number and be on a new line!

## Launcher Configuration File
The launcher configuration file contains data for the launcher to function. If the launcher cannot connect to the specified url it will not be able to patch or allow the player to start and it will not display patch notes.
```
button1name = REGISTER
button2name = PATCH NOTES
button3name = BUY COINS
button4name = DISCORD
button5name = FORUM
button1link = http://google.com
button2link = http://google.com
button3link = http://google.com
button4link = http://google.com
button5link = http://google.com
clientname = Client.exe
clientip = 127.0.0.1
launcherupdatername = LauncherUpdater.exe
launchername = Launcher.exe
patchserver = http://yourdomain/patchfile.txt
launcherpatchserver = http://yourdomain/launcherpatchfile.txt
fileserver = http://yourdomain/filecheck.txt
patchnotes = http://yourdomain/patchnote.txt
```

- Buttons and Button Links: The launcher has optional buttons. The button name will be as specified on the server and the link which the button will open will also be specified on the server. These buttons can be enabled or disabled by simply leaving the name and link empty. So, you can change them at your luxury.
- Client Name: This is the name of your executable / binary for which the launcher will try to open. If this is wrong the launcher will not be able to open the client. The client needs to be in the same location as the launcher.
- Launcher and LauncherUpdate Name: Pretty self-explanatory, the name which you would like your launcher and launcher updater to use to call each other.
- Client and Launcher Patch Server: These are the urls pointing to the client and launcher patch files. If you are unsure read the “Client and Launcher Patch Files” section. The launcher will attempt to access this url to read the patch files.
- File Server: This is the url link to the MD5 check file from the “File Check” section.
- Patch Notes: This is the url link to the patch notes file from the “Patch Notes” section.

# Section 3: Creating a Patch
The launcher can only handle 7z extension. If you don’t already have 7zip installed you can download it from here: https://www.7-zip.org/download.html
To make a patch:
1. Proceed to archive everything in your patch.
2. Upload it onto your webserver or a hosting platform.
3. Update the patchfile.txt with the following:
```
1#http://domain.com/launcher/patches/patch1.7z
```
The 1 is the version number, the # is the separator, you need to include this for the launcher to function and the url is the place where the launcher can download the patch from.

Everytime you make a new patch you simply make a new line, increase the version and link the patch file link, like so:
```
1#http://domain.com/launcher/patches/patch1.7z
2#http://domain.com/launcher/patches/patch2.7z
```
Don't forget to save the file. Once saved it should be ready. If you re-open the launcher, it will start patching.
