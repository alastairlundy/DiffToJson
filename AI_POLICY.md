# AI Contribution Policy

## Purpose
This project leverages AI to accelerate development while ensuring that engineering judgment and human accountability remain primary. We aim to prevent the erosion of code quality and maintain a clear audit trail of how logic is synthesized.

## 1. Accountability & Integrity
The human contributor is the **Author of Record**. You are responsible for:
- The correctness, security, and performance of the submitted code.
- Ensuring the code adheres to the project's design patterns and standards.
- Verifying that no licensed code (GPL, etc.) was inadvertently ingested via AI.

### Prohibition of License Washing
The use of AI for "license washing" is strictly prohibited. This includes:
- Using AI to synthesize code based on copyrighted sources to bypass license restrictions.
- Using AI to rewrite the project's licensed code into a "non-copyrightable" form for redistribution.
Any contribution found to be the result of license washing will be immediately reverted, and the contributor may be banned.

## 2. Engagement Levels
We categorize AI usage by the depth of its integration into the final codebase.

| Engagement Level | Description | Disclosure Requirement | Commit Credit |
| :--- | :--- | :--- | :--- |
| **Non-Code Assistance** | Rubber ducking, Q&A, debugging, or brainstorming. | None | N/A |
| **Light** | Basic boilerplate, renaming, or "Next Edit Suggestions" (single-line autocompletes). | PR Description | N/A |
| **Moderate** | Complex regex, utility functions, and "Code Completions" (multi-line generated blocks). | PR Description | `Assisted-by: [AI Agent] <email> - Used [Model Name]` |
| **Heavy** | Entire classes, modules, or architectural patterns. | PR Description + Code Comment | `Co-authored-by: [AI Agent] <email> - Used [Model Name]` |

*Note: Commit trailers should be placed at the end of the commit message, separated from the commit body by a single blank line. These can be added manually or by using the `--trailer` parameter during the commit process (e.g., `git commit --trailer "Assisted-by: Claude <claude@anthropic.com> - Used Claude 3.5 Sonnet" -m "Your commit message"`).*

### Restrictions on Heavy Usage
Due to the risk of architectural drift and technical debt, **Heavy** engagement is restricted to **Trusted Contributors and Maintainers**. Outside contributors should seek maintainer approval before submitting PRs where AI has generated substantial architectural components.

## 3. Quality Standards
"Low-quality AI contributions" are those that introduce:
- "Hallucinated" APIs, deprecated methods, or logically flawed patterns.
- Redundant comments or "chatty" boilerplate typical of LLMs.
- Code that lacks accompanying tests to prove correctness.

**Policy:** AI-generated code that the contributor cannot explain during a technical review will be rejected.

## 4. Process
When submitting a PR involving AI:
1. **Verify:** Run the full test suite.
2. **Clean:** Remove all AI-typical artifacts (e.g., "Here is the updated code...") from the commit.
3. **Credit:** Use the appropriate `Assisted-by` or `Co-authored-by` trailers in the commit message.
4. **Disclose:** Complete the AI Disclosure section in the PR template.
