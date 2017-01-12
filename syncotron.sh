#!/bin/bash
export MONO_IOMAP=all
cmd="mono /opt/syncotron/bin/syncotron.exe $@"

# List all processes | only those with these arguments (with fixed string -F because of "*") | but exclude the grep
ps_out=$(ps -ef | grep -F -- "${cmd}" | grep -v 'grep')

result=$(echo $ps_out)

# If there is a result then we're still running
if [[ "$result" != "" ]];then
    echo "This syncotron is already running"
    echo "($result)"
else
    echo "Starting..."
    eval $cmd
fi
