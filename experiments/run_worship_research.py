"""Reproduce collection, frozen comparison, post-hoc annotations and HTML.

Existing public-source snapshots are reused. Full lyrics remain in .tools.
The narrative report and diagnostic review notes are maintained separately.
"""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
for script in ['collect_worship_corpus.py','analyze_worship_corpus.py',
               'refine_worship_annotations.py','review_worship_spot_checks.py','render_worship_research.py']:
    subprocess.run([sys.executable, '-X', 'utf8', str(root/'experiments'/script)],cwd=root,check=True)
