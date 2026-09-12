#!/usr/bin/env bash

set -euo pipefail

mod_name="MiaoNet"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd -- "$script_dir"

build_args=(-c Release)
clean_args=()
if [[ -n "${CelesteRootPath:-}" ]]; then
  build_args+=("-p:CelesteRootPath=${CelesteRootPath}")
  clean_args+=("-p:CelesteRootPath=${CelesteRootPath}")
fi

dotnet build "${build_args[@]}"

version="$(awk '
  /^[[:space:]]*Version:[[:space:]]*/ {
    sub(/^[[:space:]]*Version:[[:space:]]*/, "")
    sub(/[[:space:]\r]+$/, "")
    print
    exit
  }
' ModFolder/everest.yaml)"

if [[ -z "$version" ]]; then
  echo "Could not read the mod version from ModFolder/everest.yaml" >&2
  exit 1
fi

archive="${mod_name} v${version}.zip"

# Compress-Archive with ModFolder/* stores the directory contents at the
# archive root. zip's -0 selects no compression and -FS replaces stale entries.
(cd ModFolder && zip -r -0 -FS "../${archive}" .)

dotnet clean "${clean_args[@]}"
