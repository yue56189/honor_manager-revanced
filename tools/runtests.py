import subprocess, sys

exe = sys.argv[1] if len(sys.argv) > 1 else r"D:\Honor_boost\tests\bin\Debug\ECControllerTests.exe"

r = subprocess.run([exe], capture_output=True, text=True, errors="replace", encoding="utf-8")
print("exitcode =", r.returncode)
print(r.stdout)
if r.stderr:
    print("---- STDERR ----")
    print(r.stderr)
