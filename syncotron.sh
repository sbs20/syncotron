#!/bin/bash
export MONO_IOMAP=all
mono /opt/syncotron/bin/syncotron.exe "$@"
