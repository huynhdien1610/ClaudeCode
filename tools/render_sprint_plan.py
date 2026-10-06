#!/usr/bin/env python3
"""Kiểm tra .vibe/backlog.json và sinh phần bảng task của docs/SPRINT_PLAN.md.

Chạy: python3 tools/render_sprint_plan.py
Phần nằm giữa hai dấu <!-- BEGIN GENERATED --> và <!-- END GENERATED --> trong
docs/SPRINT_PLAN.md được ghi đè; phần còn lại của tài liệu giữ nguyên.
"""
import fnmatch
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BACKLOG = ROOT / ".vibe" / "backlog.json"
DOC = ROOT / "docs" / "SPRINT_PLAN.md"
BEGIN, END = "<!-- BEGIN GENERATED -->", "<!-- END GENERATED -->"


def overlaps(a, b):
    """Hai glob (hỗ trợ `*` và `**`) có thể khớp cùng một file hay không."""
    sa, sb = a.strip("/").split("/"), b.strip("/").split("/")

    def seg_match(x, y):
        return fnmatch.fnmatch(x, y) or fnmatch.fnmatch(y, x)

    def walk(i, j):
        if i == len(sa) and j == len(sb):
            return True
        if i < len(sa) and sa[i] == "**":
            return walk(i + 1, j) or (j < len(sb) and walk(i, j + 1))
        if j < len(sb) and sb[j] == "**":
            return walk(i, j + 1) or (i < len(sa) and walk(i + 1, j))
        if i == len(sa) or j == len(sb):
            return False
        return seg_match(sa[i], sb[j]) and walk(i + 1, j + 1)

    return walk(0, 0)


def validate(data):
    errors, warnings = [], []
    sprints = [s["id"] for s in data["sprints"]]
    tasks = {t["id"]: t for t in data["tasks"]}
    if len(tasks) != len(data["tasks"]):
        errors.append("ID task bị trùng")
    for t in data["tasks"]:
        if t["sprint"] not in sprints:
            errors.append(f"{t['id']}: sprint {t['sprint']} không tồn tại")
        for d in t["dependencies"]:
            if d not in tasks:
                errors.append(f"{t['id']}: phụ thuộc {d} không tồn tại")
            elif sprints.index(tasks[d]["sprint"]) > sprints.index(t["sprint"]):
                errors.append(f"{t['id']}: phụ thuộc {d} nằm ở sprint sau")

    def ancestors(tid, seen=None):
        seen = seen if seen is not None else set()
        for d in tasks[tid]["dependencies"]:
            if d in tasks and d not in seen:
                seen.add(d)
                ancestors(d, seen)
        return seen

    for sp in sprints:
        group = [t for t in data["tasks"] if t["sprint"] == sp]
        for i, a in enumerate(group):
            for b in group[i + 1:]:
                if a["id"] in ancestors(b["id"]) or b["id"] in ancestors(a["id"]):
                    continue
                if a["id"] == "T097" or b["id"] == "T097":
                    continue  # buffer sửa lỗi beta chạy sau các task khác
                for fa in a["allowed_files"]:
                    for fb in b["allowed_files"]:
                        if overlaps(fa, fb):
                            warnings.append(f"{sp}: {a['id']} và {b['id']} có thể trùng file ({fa} ~ {fb}); không chạy song song")
    return errors, warnings


def render(data):
    out = [BEGIN, ""]
    for sp in data["sprints"]:
        group = [t for t in data["tasks"] if t["sprint"] == sp["id"]]
        out += [f"### {sp['id']} — {sp['name']} ({sp['weeks']})", "",
                f"**Mục tiêu:** {sp['goal']}", "", f"**Điều kiện kết thúc sprint:** {sp['exit_criteria']}", "",
                "| ID | Task | Workstream | Owner skill | Phụ thuộc | Nguồn | Tiêu chí chấp nhận | Risk | Ước lượng | Loại |",
                "|---|---|---|---|---|---|---|---|---|---|"]
        for t in group:
            out.append("| {id} | {title} | {ws} | {sk} | {deps} | {refs} | {acc} | {risk} | {est} | {gran} |".format(
                id=t["id"], title=t["title"], ws=t["workstream"], sk=t["owner_skill"],
                deps=", ".join(t["dependencies"]) or "—", refs="; ".join(t["source_refs"]),
                acc="; ".join(t["acceptance"]), risk=t["risk_tier"], est=t["estimate"],
                gran="task" if t["granularity"] == "task" else "story (tách khi lập sprint)"))
        out.append("")
    out += ["### Release 2 — epic chờ tách task", "", "| ID | Epic | Tham chiếu |", "|---|---|---|"]
    out += [f"| {e['id']} | {e['name']} | {e['refs']} |" for e in data["r2_epics"]]
    out += ["", END]
    return "\n".join(out)


def main():
    data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    errors, warnings = validate(data)
    for w in warnings:
        print("WARN", w)
    if errors:
        for e in errors:
            print("ERROR", e)
        sys.exit(1)
    doc = DOC.read_text(encoding="utf-8")
    start, end = doc.index(BEGIN), doc.index(END) + len(END)
    DOC.write_text(doc[:start] + render(data) + doc[end:], encoding="utf-8")
    print(f"OK: {len(data['tasks'])} task, {len(data['sprints'])} sprint, {len(warnings)} cảnh báo")


if __name__ == "__main__":
    main()
