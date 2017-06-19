{ pkgs ? import <nixpkgs> {} }:

with pkgs;

stdenv.mkDerivation rec {
  version = "1.0";
  name = "netuv-${version}";
  buildDependencies = [ libuv ];
  libPath = lib.makeLibraryPath buildDependencies;
  shellHook = ''
    export LD_LIBRARY_PATH="$libPath":$LD_LIBRARY_PATH
  '';
}
