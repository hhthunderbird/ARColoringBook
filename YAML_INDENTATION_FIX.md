# YAML Indentation Fix

## Error Found

**Location:** Line 24 in `.github/workflows/build-and-package.yml`

**Issue:** Missing indentation in the `env` section

```yaml
# ? WRONG - No indentation
env:
UNITY_VERSION: '6000.0.30f1'
PACKAGE_NAME: 'FelinaARColoringBook'
IOS_LIBRARY_NAME: 'Felina'

# ? CORRECT - Proper indentation (2 spaces)
env:
  UNITY_VERSION: '6000.0.30f1'
  PACKAGE_NAME: 'FelinaARColoringBook'
  IOS_LIBRARY_NAME: 'Felina'
```

## Why This Causes Errors

YAML (the format used for GitHub Actions workflows) is **indentation-sensitive**. The indentation defines the structure and relationships between elements:

- Top-level keys (like `env:`, `jobs:`, `on:`) should have **no indentation**
- Values under those keys need **2 spaces** of indentation
- Nested values need additional **2 spaces** for each level

### Impact of This Error:

Without proper indentation, GitHub Actions couldn't parse the YAML file correctly, which would cause:
- ? Workflow validation errors
- ? Variables not being set properly
- ? Potential runtime failures

## Fixed

```yaml
env:
  UNITY_VERSION: '6000.0.30f1'
  PACKAGE_NAME: 'FelinaARColoringBook'
  IOS_LIBRARY_NAME: 'Felina'
```

## YAML Indentation Rules

### Top-Level Structure
```yaml
name: Workflow Name        # No indentation
on:                        # No indentation
  push:                    # 2 spaces
    branches:              # 4 spaces
      - main               # 6 spaces
env:                       # No indentation
  VAR_NAME: 'value'        # 2 spaces
jobs:                      # No indentation
  job-name:                # 2 spaces
    runs-on: ubuntu-latest # 4 spaces
```

### Common Mistakes

```yaml
# ? WRONG - Missing indentation
env:
VARIABLE: value

# ? WRONG - Too much indentation
env:
    VARIABLE: value

# ? WRONG - Inconsistent indentation
env:
  VARIABLE1: value
   VARIABLE2: value        # 3 spaces instead of 2

# ? CORRECT - Consistent 2-space indentation
env:
  VARIABLE1: value
  VARIABLE2: value
```

## How to Prevent This

1. **Use a YAML validator**: Most code editors have YAML validation
2. **Enable format-on-save**: Configure your editor to auto-format YAML
3. **Use YAML linters**: Tools like `yamllint` or GitHub's workflow validator
4. **Consistent spaces**: Always use 2 spaces for indentation (never tabs)

## VS Code Settings (Recommended)

Add to your `.vscode/settings.json`:

```json
{
  "[yaml]": {
    "editor.insertSpaces": true,
    "editor.tabSize": 2,
    "editor.autoIndent": "advanced",
    "editor.formatOnSave": true
  },
  "yaml.schemas": {
    "https://json.schemastore.org/github-workflow.json": ".github/workflows/*.yml"
  }
}
```

This will:
- ? Use 2 spaces for YAML indentation
- ? Auto-format on save
- ? Validate GitHub Actions workflows
- ? Provide autocomplete for workflow properties

## Verification

To check if your YAML is valid:

### Local Validation
```bash
# Using yamllint
yamllint .github/workflows/build-and-package.yml

# Using GitHub CLI
gh workflow view
```

### Online Validation
- [YAML Lint](http://www.yamllint.com/)
- [GitHub Actions Workflow Syntax](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions)

## Commit This Fix

```bash
git add .github/workflows/build-and-package.yml
git commit -m "fix(ci): correct YAML indentation in env section

- Add proper 2-space indentation to environment variables
- Fix UNITY_VERSION, PACKAGE_NAME, IOS_LIBRARY_NAME alignment
- Resolve YAML parsing error"

git push origin feature/store-version
```

---

**Status**: Fixed! The workflow file now has correct YAML formatting. ?
