# Syncotron

Use Syncotron to sync your files with Dropbox (and in the future, other providers).

## Why?
Syncotron is platform neutral. It runs on x86 or ARM hardware. All you need is a .NET
runtime which can be mono. I wrote this to run on my Raspberry Pi and sync to my NAS.
It supports

 * Mirror Download (just copy what's on the server)
 * Mirror Upload (just upload what's on the local machine)
 * Two way (keeps track of updates and deletions)

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
	* Certify - if you've manually downloaded all files already and are confident that everything
	  is in sync, then by running certify you can avoid re-downloading / uploading everything so
	  that Autosync will work. Certify examines all files and takes a hash to store in its index

 * ConflictStrategy
    * None - do nothing (conflict remains)
	* RemoteWin - server always wins
	* LocalWin - local always wins
	* LatestWin - compares date modification stamps and uses the newest
	* KeepBoth - downloads, does a hash comparison, if different keeps both, otherwise resets hashes

 * Exclusions
	* An array of patterns to avoid. Useful for ignoring certain paths or files

## Running on Windows
TODO

## Running on debian linux
sudo apt-get install mono-complete

See also:
http://logicalgenetics.com/raspberry-pi-and-mono-hello-world/
http://www.mono-project.com/docs/getting-started/mono-basics/

## Example command lines

### Fast and minimum load

[TODO]

[Downsides]

### Slow but thorough

[TODO]

[Downsides]