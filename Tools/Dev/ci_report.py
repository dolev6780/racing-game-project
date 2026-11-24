"""
Reads a saved CI log of the "Unity play mode tests" job (the JSON file the GitHub MCP get_job_logs tool saves when
the log is too big to show) and prints what matters: each failed test with its message (the touch test's places one
per line, also written to OUT/touch.txt), the totals, the rock steps found, and every PERF line (what each picture
drew and its heaviest things). Then extracts the CI pictures with ci_parse.py into OUT.

    python3 -I Tools/Dev/ci_report.py SAVED_LOG.txt OUT_DIR
"""
import json, os, re, subprocess, sys

log, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
raw = open(log).read()
try:
    t = json.loads(raw)["logs_content"]
except Exception:
    t = raw
for m in re.finditer(r"CI FAILURE AGAIN: ([^\n]*)\n([^\n]*)\n([^\n]*)\n([^\n]*)", t):
    name = m.group(1).split(".")[-1]
    msg = re.sub(r"^\S+Z\s+", "", m.group(2))
    if "RajaTouches" in name:
        head, _, rest = msg.partition(": ")
        items = rest.split(" | ")
        print(name, head)
        print("\n".join(items))
        open(os.path.join(out, "touch.txt"), "w").write("\n".join(items))
    else:
        print(name, "::", msg[:300], "||", re.sub(r"^\S+Z\s+", "", m.group(3))[:200], "||", re.sub(r"^\S+Z\s+", "", m.group(4))[:250])
m = re.search(r"TESTS DONE[^\n]*", t)
print(m.group(0) if m else "no TESTS DONE line (the run may have stopped early)")
for m in re.finditer(r"TOUCH 1-\d[^\n]*", t):
    print(m.group(0))
seen = set()
for m in re.finditer(r"PERF ([^\n]*)", t):
    if m.group(1) not in seen:
        seen.add(m.group(1))
        print("PERF", m.group(1)[:260])
here = os.path.dirname(os.path.abspath(__file__))
subprocess.run([sys.executable, "-I", os.path.join(here, "ci_parse.py"), log, out])
