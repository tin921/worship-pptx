# v0.93.38 release records

The Windows installer and portable ZIP are built on E: from the commit recorded
in `build-source.json`. `package-validation.json` records the extracted MSI and
ZIP payload comparison. No native MSI installation is performed.

`tests.log` preserves the 162 passing Windows checks from the font update.
`package.ps1` builds and packages this release in a fresh E: workspace;
`verify.ps1` compares every packaged file against that build. The extra Ministry
of Education licence terms are included alongside the font notice.

`publish_release.py` requires the dependency in `requirements.txt`, a configured
Git credential helper, verified packages, and a clean, pushed main branch. It
publishes the exact built commit to `tin921/worship-pptx`; credentials remain in
memory. Run it only when publication is intended. `publication.json` records
asset hashes and anonymous download checks after publication.

The existing English tutorial remains linked from the README and v0.93.37.
