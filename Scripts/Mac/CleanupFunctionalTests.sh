. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

pkill -9 -l Scalar.FunctionalTests
pkill -9 -l git
pkill -9 -l scalar
pkill -9 -l Scalar.Mount

if [ -d /Scalar.FT ]; then
    sudo rm -r /Scalar.FT
fi
