# CONTRIBUTING.md

Thank you for your interest in contributing to DiffToJson! To ensure a high-quality codebase and a sustainable maintainer workload, please follow these guidelines.

## 1. Getting Started: The "Issue First" Workflow

We prioritize a collaborative design process. To prevent "drive-by" PRs that may not align with the project's architectural goals, we follow an **Issue First** approach.

### For Features and Major Fixes
1. **Open an Issue:** Before writing any code, create an issue to describe the problem or the feature you want to implement.
2. **Discuss & Align:** Work with the maintainers in the issue thread to agree on the approach and implementation details.
3. **Implement:** Only once the approach is approved should you begin development.

### The "Fast Track" for Trivial Bugs
Maintainers may use discretion to accept high-quality unsolicited PRs for trivial matters, such as:
- Typo fixes in documentation or comments.
- Critical bug fixes for obvious regressions.
- Simple configuration updates.

## 2. PR Sizing and Scope

To make reviews efficient and reduce the risk of regressions, we enforce a strict sizing rule:

- **One Logical Change per PR:** Each Pull Request must address exactly one feature, one bug, or one cohesive refactoring task.
- If your change feels too large, consider breaking it into a series of smaller, sequential PRs.

## 3. Testing Requirements

Quality is non-negotiable. Most contributions must be verified by the test suite.

- **New Features:** Must always include new tests that prove the functionality and cover edge cases.
- **Bug Fixes (Existing Functionality):** 
    - You do not need to create new tests if the fix resolves a failure in an existing test.
    - You must ensure that all tests that previously passed still pass, and any failing tests relevant to the bug now succeed.
- **Documentation-Only Fixes:** Fixes that exclusively address documentation do not require the test suite to be run or pass.
- **Other Trivial Fixes:** Fixes to typos in code or comments (that are not exclusively documentation) still require the full test suite to pass.

## 4. AI Contribution Policy

This project permits the use of AI to accelerate development, provided it does not erode engineering judgment. **All contributions must strictly comply with the [AI_POLICY.md](./AI_POLICY.md).**

### Integration into Workflow
- **Disclosure & Credit:** Use the required `Assisted-by` or `Co-authored-by` commit trailers and complete the AI Disclosure section in the PR template.
- **Cleaning:** Remove all AI-typical artifacts ("Here is the code...") before committing.
- **Verification:** You are the Author of Record. You must be able to explain every line of AI-generated code during review.

### Handling Complex AI Tasks (Slicing)
The `AI_POLICY.md` restricts "Heavy" AI engagement (generating entire modules or architectural patterns) to Trusted Contributors. 

If you are an outside contributor implementing a complex feature:
- **Slice your contribution:** Break the feature into smaller "Light" or "Moderate" AI tasks across multiple PRs.
- **Seek Approval:** If a specific architectural leap requires "Heavy" AI usage, you must explicitly request and receive maintainer approval during the "Issue First" discussion phase.

## 5. Summary Checklist before Submission
- [ ] I have opened an issue and received approval (or this is a Fast Track fix).
- [ ] My PR contains only one logical change.
- [ ] New features have new tests; logic fixes ensure tests pass.
- [ ] I have adhered to the `AI_POLICY.md` (Trailers added, artifacts removed).
- [ ] I can technically explain all changes in this PR.
