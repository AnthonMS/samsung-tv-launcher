"""Prepare a guarded, reversible guide bridge for the inspected Moonfin 2.10.0 build.

Does not connect to a TV or install files. Never edits account data. Reuses the
existing remote-interaction cleanup and guide callbacks from Moonfin-Client/Smart-TV.
"""
import argparse
import hashlib
from pathlib import Path
import re

SUPPORTED_MAIN = "554ca7733a19a0ec26ac22ad296c39edefba65d5dc0ae9f1bab92e2e014f3074"


def patch_main(data: bytes) -> bytes:
    if hashlib.sha256(data).hexdigest() != SUPPORTED_MAIN:
        raise ValueError("Uninspected Moonfin bundle; refusing to patch a different build")
    text = data.decode("utf-8")
    guide = re.findall(r"onGuide:([$\w]+)", text)
    interaction = re.findall(r"notifyInteraction:([$\w]+)", text)
    context = list(re.finditer(
        r"goToSearch:function\([^)]*\)\{(?P<clear>[$\w]+)\(\),[^}]*?,"
        r"(?P<nav>[$\w]+)\((?P<panels>[$\w]+)\.SEARCH,(?P<panel>[$\w]+)!==", text))
    if len(guide) != 1 or len(interaction) != 1 or len(context) != 1:
        raise ValueError("Moonfin navigation shape changed")
    c = context[0].groupdict()
    callback = ("function(){if(" + "||".join(c["panel"] + "===" + c["panels"] + "." + name
                for name in ("LOGIN", "ADD_SERVER", "ADD_USER")) + ")return false;"
                + c["clear"] + "();" + guide[0] + "();return true;}")
    needle = "notifyInteraction:" + interaction[0]
    api = re.findall(r"([$\w]+)\.getLiveTvChannels\(0,1\)", text)
    play = re.findall(r"playItems:([$\w]+)", text)
    current = re.findall(r"children:"+re.escape(c["panel"])+r"==="+re.escape(c["panels"])+r"\.PLAYER&&([$\w]+)&&", text)
    if len(api)!=1 or len(play)!=1 or len(current)!=1:
        raise ValueError("Moonfin channel callbacks changed")
    blocked="||".join(c["panel"]+"==="+c["panels"]+"."+n for n in ("LOGIN","ADD_SERVER","ADD_USER"))
    if len(re.findall(r"onPlayChannel:yo",text))!=2 or text.count("onPlayNext:Ro")!=1 or "case 3:return e.n=4,ae.D0();" not in text:
        raise ValueError("Moonfin live player cleanup changed")
    channel=("{ready:function(){return !("+blocked+");},scope:"+api[0]+",sort:y.liveTvChannelSortBy,"
             "list:function(){return "+api[0]+".getLiveTvChannels();},current:function(){return "+current[0]+"&&"+current[0]+".Id;},"
             "play:function(channel,lineup){"+c["clear"]+"();var start=function(){if("+c["panel"]+"==="+c["panels"]+".PLAYER)Ro(channel);else yo(channel,lineup);};if("+c["panel"]+"==="+c["panels"]+".PLAYER)return ae.D0().then(start);start();return Promise.resolve();}}")
    replacement = "notifyInteraction:(window.__quietMoonfinOpenGuide=" + callback + ",window.__quietMoonfinChannels="+channel+"," + interaction[0] + ")"
    return text.replace(needle, replacement, 1).encode("utf-8")


def patch_index(data: bytes) -> bytes:
    tag = b'<script defer="defer" src="main.js"></script>'
    if data.count(tag) != 1 or b"quiet-guide.js" in data:
        raise ValueError("Unexpected or already modified Moonfin index")
    return data.replace(tag, tag + b'<script defer="defer" src="quiet-guide.js"></script>')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("main", type=Path)
    parser.add_argument("index", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    main_data, index_data = patch_main(args.main.read_bytes()), patch_index(args.index.read_bytes())
    args.output.mkdir(parents=True, exist_ok=False)
    (args.output / "main.js").write_bytes(main_data)
    (args.output / "index.html").write_bytes(index_data)
    (args.output / "quiet-guide.js").write_bytes((Path(__file__).resolve().parents[1] / "launcher/moonfin/quiet-guide.js").read_bytes())
    for path in sorted(args.output.iterdir()):
        print(hashlib.sha256(path.read_bytes()).hexdigest(), path.name)


if __name__ == "__main__":
    main()
