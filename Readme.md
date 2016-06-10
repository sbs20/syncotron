﻿# Syncotron

Use Syncotron to sync your files with Dropbox (and in the future, other providers).

## Why?
Syncotron is platform neutral. It runs on x86 or ARM hardware. All you need is a .NET
runtime which can be mono. I wrote this to run on my Raspberry Pi and sync to my NAS.
It supports

 * Mirror Download (just copy what's on the server)
 * Mirror Upload (just upload what's on the local machine)
 * Two way (keeps track of updates and deletions)

Syncotron runs once and synchronises all files and then exits. It is *not* a real-time
dameon or service which runs continually in the background.

In time it may run with .NET Core - but not today.

## Options
 * Specify a LocalPath and RemotePath pair. The local path can be anywhere you have permissions
   including a remote file share

 * Choose your hashing strategy. By default, Syncotron uses a hash of the size and last-
   modified date for performance on slow systems, but it also supports slower MD5 content hashing.

 * Specify a CommandMode.
    * AnalysisOnly - examines the files and doesn't do anything
	* Fullsync - always scans and compares all files
	* Autosync - scans and compares all files then subsequently scans and compares changes
	* Reset - clears local cursor information - Autosync will revert to a Fullscan once
	* CertifyStrict - if you've manually downloaded all files already and are confident that everything
	  is in sync, then by running certify you can avoid re-downloading / uploading everything so
	  that Autosync will work. Certify examines all files and takes a hash to store in its index.
	  Strict mode will fail unless all files are exactly present on both sides.
	* CertifyLiberal - as per strict, but only matches (same size) are certified. Everything else is
	  left so that the next sync job will do something about it.
	* Continue - sets the local and remote cursors so that subsequent Autosync will just apply changes
	  only. This is useful if you are not too bothered about having a full local index of files and you
	  are running in MirrorDown direction

 * ConflictStrategy
    * None - do nothing (conflict remains)
	* RemoteWin - server always wins
	* LocalWin - local always wins
	* LatestWin - compares date modification stamps and uses the newest
	* KeepBoth - downloads, does a hash comparison, if different keeps both, otherwise resets hashes

 * Exclusions
	* An array of patterns to avoid. Useful for ignoring certain paths or files

## Install on Windows
TODO

## Install on debian linux
sudo apt-get install mono-complete
TODO

http://logicalgenetics.com/raspberry-pi-and-mono-hello-world/
http://www.mono-project.com/docs/getting-started/mono-basics/

## Example command lines

### Simple download example.
Sync up the remote /Documents directory. The first run will perform a full index and download.
Subsequent runs will use a remote cursor to apply changes only
```
syncotron.exe -LocalPath \\storage\Dropbox\Documents -RemotePath /Documents -CommandType Autosync -SyncDirection MirrorDown
```

### Full re-sync download
If you are concerned that Autosync has drifted out of alignment (perhaps through accidental 
deletion of local files) then just run a one off Fullsync. If you don't have full control of
your local drive then this may be worth doing. It is slower than Autosync / Continue.
```
syncotron.exe -LocalPath \\storage\Dropbox\Documents -RemotePath /Documents -CommandType Fullsync -SyncDirection MirrorDown
```

### Certify
Where local files exist and you know for certain that they are the same as the remote versions.
You want to avoid forcing a full download of all files again. Run a one off CertifyLiberal
and then switch to Autosync. This works by building a full index of all remote and local files
and their hashes so that subsequent changes can be easily spotted. Essentially this is *you*
certifying to the system that the remote hash and local hash can be associated.
```
syncotron.exe -LocalPath \\storage\Dropbox\Documents -RemotePath /Documents -CommandType CertifyLiberal -SyncDirection MirrorDown
```

### Continue
You know all the local files are in sync (or don't care that they're not). And you just want
to get straight to "changes only". The advantage of this approach is that it's a really fast
shortcut. The downside is that if you need to switch to a Fullsync to ensure that everything
is the same then Fullsync will not find local index entries (with file hashes) and will end
up downloading a whole load of unnecessary data. If so - see Certify.
```
syncotron.exe -LocalPath \\storage\Dropbox\Documents -RemotePath /Documents -CommandType Continue -SyncDirection MirrorDown
```

### Upload data
Sync up the local /Documents directory to remote. The first run will perform a full index and 
upload. Subsequent runs will rescan the local disk and compare it to previous runs to calculate
differences to apply changes only
```
syncotron.exe -LocalPath \\storage\Documents -RemotePath /Documents -CommandType Autosync -SyncDirection MirrorUp
```

### Recover from "Replicator already running"
If a previous job has failed unexpectedly then it's possible that syncotron did not have a 
chance to clean up. If so add the -Recover switch.
```
syncotron.exe -LocalPath c:\Documents -RemotePath /Documents -CommandType Autosync -SyncDirection MirrorUp -Recover
```

### Two way sync
This is more like standard Dropbox. Syncotron tracks deletions so will delete remotely if
needed. Because of the way syncotron tracks deletions this can be quite slow if you are
syncing a lot of files and running on a slow computer. It takes around 10 minutes to do a 
full scan of 100k files on a remote NAS running on a Raspberry PI 2. An i7 with a local SSD
is considerably faster.
```
syncotron.exe -LocalPath c:\Documents -RemotePath /Documents -CommandType Autosync -SyncDirection TwoWay
```

### Running on Linux
This requires Mono to run. Furthermore, because of problems with Dropbox not being entirely
consistent with its reporting of file paths with respect to case, you MUST set the following
environment variable MONO_IOMAP. You may also experience problems with SSL certificates if
running on older versions of Mono (e.g. that on RPi) -- use -IgnoreCertificateErrors if you
are comfortable doing so. Please note that *this is a security risk*. You can place the 
following in a script file and mark it executable.

```
#!/bin/bash
export MONO_IOMAP=all
mono ~/syncotron/syncotron.exe -LocalPath /mnt/storage/dropbox -RemotePath / -CommandType Autosync -SyncDirection MirrorDown -IgnoreCertificateErrors
```
