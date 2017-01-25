#!/bin/bash
#
# This start script for Docker ensures that we do not run Ceen as the root user
#
adduser --disabled-password --gecos 'Ceen Autogenerated User' ceenuser
CMD="$@" #Expand the commandline so we can pass it as a string
su -m ceenuser -c "$CMD"