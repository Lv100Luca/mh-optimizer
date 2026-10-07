#!/usr/bin/env bash
# Follow-up to sweep-subsolvers.sh: one worker with the full LP relaxation (linearization_level 2) did as well as many
# default workers, so check it on every model and next to a few worker counts.
cd "$(dirname "$0")"
run() { dotnet run --project Native -c Release --no-build -- solve out/models "$1" 60 "" "$2"; }
s() { local out=""; for x in "$@"; do out+="subsolvers:'$x', "; done; echo "${out%, }"; }

run 1 "linearization_level:2"
run 1 "linearization_level:2, search_branching:LP_SEARCH"
run 2 "$(s max_lp reduced_costs)"
run 2 "linearization_level:2, $(s max_lp quick_restart)"
run 4 "$(s max_lp reduced_costs quick_restart default_lp)"
