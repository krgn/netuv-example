{ pkgs ? import <nixpkgs> {} }:

with pkgs;

let
  libuv191 = libuv.overrideDerivation (attrs: rec {
    version = "1.9.1";
    src = fetchurl {
        url = "https://github.com/libuv/libuv/archive/v${version}.tar.gz";
        sha256 = "1bapdkj3jmd10bl34jzhwb6fgjyx8vjmqjbbyiii8gcp9039zjm6";
    };
  });

in stdenv.mkDerivation rec {
  version = "1.0";
  name = "netuv-${version}";
  buildDependencies = [ libuv191 ];
  libPath = lib.makeLibraryPath buildDependencies;
  shellHook = ''
    export LD_LIBRARY_PATH="$libPath":$LD_LIBRARY_PATH
  '';
}
