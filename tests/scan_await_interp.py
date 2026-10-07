#!/usr/bin/env python3
"""扫描：async 方法内、await 之后，日志语句里插值了 Godot 对象的地方。

这是 BUGS #60 那个崩溃的形状：引用跨过 await 之后节点可能已被释放，
而 `$"{card}"` 之类的插值要走原生指针，对已释放的包装直接抛
ObjectDisposedException。本脚本只做排查，不参与断言。
"""
import io
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

INTERP = re.compile(r'\$"(?:[^"\\]|\\.)*"')
IDENT = re.compile(r"\{([A-Za-z_][A-Za-z0-9_.\[\]()]*)\}")
PRIM = re.compile(r"^(\d|result|count|Count|index|i$|n$|turn|hp|Cost|cost$)")

SKIP_DIRS = {".git", ".godot", "addons", "tests"}


def methods(text):
    """粗切方法体：async 签名 + 大括号配平。"""
    for m in re.finditer(r"(\basync\s+(?:Task|ValueTask|void)[^\n{;]*)\n?\s*\{", text):
        brace = text.index("{", m.start())
        depth, i = 0, brace
        while i < len(text):
            if text[i] == "{":
                depth += 1
            elif text[i] == "}":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        yield m.group(1).strip(), text[brace:i + 1], text[:m.start()].count("\n") + 1


def is_primitive(ident):
    if PRIM.match(ident):
        return True
    if ident.endswith(".Count") or ident.endswith(".Length"):
        return True
    return ident.isdigit()


def main():
    hits = []
    for root, dirs, files in os.walk("."):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(root, name).replace(os.sep, "/").lstrip("./")
            text = io.open(os.path.join(root, name), encoding="utf-8", errors="replace").read()
            for sig, body, base in methods(text):
                seen_await = False
                for offset, line in enumerate(body.split("\n")):
                    if "await " in line:
                        seen_await = True
                        continue
                    if not seen_await:
                        continue
                    if not re.search(r"\bGD\.(Print|PushWarning|PushError)", line):
                        continue
                    for raw in INTERP.findall(line):
                        for ident in IDENT.findall(raw):
                            if is_primitive(ident) or ident == "this":
                                continue
                            hits.append((path, base + offset, sig[:58], line.strip()[:104]))

    print(f"await 之后插值对象的日志共 {len(hits)} 处\n")
    for path, line_no, sig, line in hits:
        print(f"  {path}:{line_no}")
        print(f"      {sig}")
        print(f"      {line}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
