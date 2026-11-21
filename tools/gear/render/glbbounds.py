import json,struct,sys
for p in sys.argv[1:]:
    b=open(p,"rb").read(); n=struct.unpack("<I",b[12:16])[0]; j=json.loads(b[20:20+n])
    for m in j["meshes"]:
        for pr in m["primitives"]:
            a=j["accessors"][pr["attributes"]["POSITION"]]; print(p.split('/')[-1], m.get("name"), "min",[round(x,3) for x in a["min"]],"max",[round(x,3) for x in a["max"]])
    for nd in j["nodes"]:
        if any(k in nd for k in ("rotation","translation","scale","matrix")): print("  node",nd.get("name"),{k:nd[k] for k in ("rotation","translation","scale","matrix") if k in nd})
