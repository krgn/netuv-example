# NetUV.Core Example

A quick conversion of the examples
located [here](https://github.com/StormHub/NetUV/tree/dev/examples),
namely the `EchoServer` and `EchoClient` projects
to [FSharp](http://fsharp.org).

## Building

```shell

$ .\build.cmd # on windows
$ ./build.sh  # on *nix

```

## Running

```shell
cd build/

mono ./NetUv.Server.exe &
mono ./NetUv.Client.exe 

```
