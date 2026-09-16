import subprocess, sys, os, shutil

proj = sys.argv[1] if len(sys.argv) > 1 else r"D:\Honor_boost\ECController\ECController\ECController.csproj"
cfg = sys.argv[2] if len(sys.argv) > 2 else "Debug"
tgt = sys.argv[3] if len(sys.argv) > 3 else "Rebuild"

sdk_res = r"C:\Program Files\dotnet\sdk\10.0.301\System.Resources.Extensions.dll"
libdir = r"D:\Honor_boost\libs"
os.makedirs(libdir, exist_ok=True)
dst = os.path.join(libdir, "System.Resources.Extensions.dll")
if not os.path.exists(dst):
    shutil.copy2(sdk_res, dst)

args = ["dotnet", "msbuild", proj, "/t:" + tgt, "/p:Configuration=" + cfg,
        "/v:minimal", "/nologo",
        "/p:GenerateResourceUsePreserializedResources=true",
        "/p:ReferencePath=" + libdir]

r = subprocess.run(args, capture_output=True, text=True, errors="replace", encoding="utf-8")
print("exitcode =", r.returncode)
print("---- STDOUT ----")
print(r.stdout)
print("---- STDERR ----")
print(r.stderr)
