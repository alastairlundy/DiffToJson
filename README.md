# GitDiffToJsonLCli

A CLI for detecting and serializing Git commit Diffs and commit messages from a local Git repository to a JSONL file.

This can be useful for preparing git commit diffs and message data for training AI/ML models or similar use cases.

**NOTE**: Whilst the CLI implements a Regex pattern matching based PII detector for detecting email addresses in Commit Messages and redacting them, redaction of email addresses is not guaranteed. If commit messages contain sensitive information conduct a human review theoutput JSONL file.

## Documented Information
The following is provided in the output JSONL files:
* The Git Diff
* The Git Commit Message associated with the diff
* The License Name if a LICENSE.txt, LICENSE.md, or LICENSE.txt file is present in the repo directory - An LLM call is required to compute this. As a fallback "Unknown" is returned otherwise.
* The Git project name - Obtained from the Git Repo Directory name
* The Git Repo URL if provided by the CLI called.

## License
This project contains AI generated code and human written code. All human written code in this project is licensed under the Apache 2.0 license.
