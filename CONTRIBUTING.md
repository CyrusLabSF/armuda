# Contributing to Armuda

Thank you for helping develop Armuda.

1. Keep navigation consistent: left click selects, right click opens attachments/actions, and middle-button drag controls camera look.
2. Keep runtime data out of source control. Never commit profiles, worlds, logs, uploads, API keys, or provider tokens.
3. Run the unit suite before opening a change:

   ```powershell
   $env:PYTHONUTF8='1'
   python -m unittest discover -s "Armuda World Directory Map/Armuda/tests" -p "test_*.py" -v
   ```

4. For visual changes, also run `run_panel_visual_smoke.py` and inspect its captures at the supported minimum window size.
5. Explain user-facing interaction changes in the pull request.

By submitting a pull request, contributors must affirm the terms in `CONTRIBUTOR_LICENSE_AGREEMENT.md`. Code contributions are received under that agreement and the applicable MPL-2.0 outbound scope; creative content is not automatically open-sourced.
