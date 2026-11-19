#!/usr/bin/env bash
# Checks a pull request's shaders with tussen-shader-check, which compiles
# each one exactly as tussen does and applies the house rules in
# skills/tussen-shaders/.
#
#   check-shaders.sh <checker> <base commit> <head commit>
#
# - The shaders the pull request adds or changes: --strict, so a broken
#   house rule fails, annotated on the diff at its line.
# - Every other shader: warnings only (older shaders predate some rules),
#   in the log; a compile error still fails.
# - More than one shader changed fails: one shader per pull request.
checker=$1
base=$2
head=$3
status=0

changed=()
while IFS= read -r f; do changed+=("$f"); done < <(git diff --name-only "$base...$head" -- 'shaders/*.fs')

present=()
for f in "${changed[@]}"; do
  if [ -f "$f" ]; then present+=("$f"); fi
done
if [ ${#present[@]} -gt 0 ]; then
  echo "This pull request's shader, held to every house rule (--strict):"
  "$checker" --strict --github "${present[@]}" || status=1
fi

rest=()
for f in shaders/*.fs; do
  mine=0
  for c in "${changed[@]}"; do
    if [ "$f" = "$c" ]; then mine=1; fi
  done
  if [ $mine = 0 ]; then rest+=("$f"); fi
done
if [ ${#rest[@]} -gt 0 ]; then
  echo "::group::Every other shader (house rules warn only)"
  "$checker" "${rest[@]}" || status=1
  echo "::endgroup::"
fi

if [ ${#changed[@]} -gt 1 ]; then
  echo "::error::This pull request changes ${#changed[@]} shaders (${changed[*]}): one shader per pull request, see CONTRIBUTING.md"
  status=1
fi
exit $status
