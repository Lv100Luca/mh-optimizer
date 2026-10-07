#!/usr/bin/env bash
# Solves the saved models with different worker counts and subsolver lineups (time limit 60 s per model).
# One worker is slow (~90 s a solve by default), so those runs only take the first model of each config.
cd "$(dirname "$0")"
run() { dotnet run --project Native -c Release --no-build -- solve out/models "$1" 60 "$3" "$2"; }
s() { local out=""; for x in "$@"; do out+="subsolvers:'$x', "; done; echo "${out%, }"; }

run 1 "linearization_level:2" "-1."
run 2 "" "-1."
run 2 "$(s max_lp reduced_costs)"
run 3 "$(s max_lp reduced_costs quick_restart)"
run 4 "$(s max_lp reduced_costs quick_restart core)"
run 4 "$(s max_lp reduced_costs pseudo_costs quick_restart)"
run 6 "$(s max_lp reduced_costs quick_restart default_lp core pseudo_costs)"
run 8 ""
run 8 "$(s max_lp reduced_costs quick_restart default_lp core no_lp pseudo_costs lb_tree_search)"
run 16 ""
