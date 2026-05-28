# Context

This document serves as a glossary and domain map for the DiffToJson project. It contains canonical terms and their definitions, stripped of implementation details.

## Core Domain Terms

### Git Diff
The set of changes introduced by a commit, including added, modified, and deleted lines, and the metadata associated with the affected files.

### JSONL (JSON Lines)
A text format where each single line is a valid JSON object, used here to represent individual commit diffs and their associated metadata for ML training.

### License Detection
The process of identifying the legal license governing a codebase by analyzing `LICENSE*` files, which informs the metadata of the generated JSONL.

### PII Redaction
The identification and removal of Personally Identifiable Information (specifically email addresses) from commit messages to ensure the dataset complies with privacy standards.
