import json,struct,sys
for p in sys.argv[1:]:
    b=open(p,"rb").read()
    n=struct.unpack("<I",b[12:16])[0]; j=json.loads(b[20:20+n])
    print(p)
    print(" meshnodes",[nd.get("name") for nd in j["nodes"] if "mesh" in nd])
    print(" mats",[m["name"] for m in j.get("materials",[])])
    for sk in j.get("skins",[]): print(" bones",len(sk["joints"]), [j["nodes"][i]["name"] for i in sk["joints"]])
    print(" extras", [ (nd.get("name"),nd.get("extras")) for nd in j["nodes"] if nd.get("extras")], j.get("scenes",[{}])[0].get("extras"))
