# Code Quality Automation

This repository includes automated code quality checks to maintain best practices and prevent technical debt.

## 📋 Standards

### File Length
- **Maximum**: 1,000 lines (hard limit, push will fail)
- **Recommended**: Under 500 lines
- **Exclusions**: Auto-generated files (Migrations, wwwroot/lib)

### Class Length
- **Maximum**: 500 lines (hard limit)
- **Recommended**: Under 300 lines

### Rationale
Following these limits ensures:
- Better readability and maintainability
- Easier testing and debugging
- SOLID principles (Single Responsibility)
- Reduced cognitive load

## 🔧 Setup

### Install Git Hooks (One-Time Setup)

```bash
./scripts/install-hooks.sh
```

This installs:
- **Pre-push hook**: Runs code quality checks before every `git push`

### Manual Check

Run the code quality check manually:

```bash
./scripts/check-code-length.sh
```

## 🚀 Automation

### Git Pre-Push Hook
- **Runs**: Every time you push to remote
- **Action**: Blocks push if standards are violated
- **Bypass**: Use `git push --no-verify` (not recommended)

### GitHub Actions (CI/CD)
The workflow runs automatically:

#### On Push/Pull Request
- Validates code length standards
- Runs .NET build and tests
- Generates code metrics summary

#### Weekly Audit (Every Monday 9 AM UTC)
- Performs full codebase analysis
- **Creates GitHub Issue** if violations are found
- Labels: `code-quality`, `automated`, `refactoring-needed`

#### Manual Trigger
- Go to Actions → Code Quality Check
- Click "Run workflow"

## 📊 What Gets Checked

### Analyzed Files
- `*.cs` (C# source files)
- `*.razor` (Blazor components)

### Excluded Directories
- `Migrations/` - Auto-generated
- `bin/`, `obj/` - Build output
- `wwwroot/lib/` - Third-party libraries
- `.git/`, `node_modules/` - VCS and packages

## ⚠️ Handling Violations

### If Check Fails

1. **Review the output** - See which files exceed limits
2. **Refactor the code**:
   ```
   ❌ FAIL: src/Services/LargeService.cs (1200 lines > 1000 max)
   ```
   - Extract helper classes
   - Split into multiple focused classes
   - Apply Single Responsibility Principle
   - Consider using partial classes if appropriate

3. **Re-run the check**:
   ```bash
   ./scripts/check-code-length.sh
   ```

4. **Push again** once violations are fixed

### Example Refactoring

**Before (600 lines)**:
```csharp
public class MonolithService
{
    // 600 lines of mixed responsibilities
}
```

**After**:
```csharp
// Parser.cs (150 lines)
public class DataParser { ... }

// Validator.cs (120 lines)  
public class DataValidator { ... }

// Saver.cs (100 lines)
public class DataSaver { ... }

// Orchestrator.cs (180 lines)
public class DataOrchestrator
{
    private readonly DataParser _parser;
    private readonly DataValidator _validator;
    private readonly DataSaver _saver;
    // Coordinates the components
}
```

## 🔕 Disabling Checks (Not Recommended)

### Skip Pre-Push Hook
```bash
git push --no-verify
```

### Remove Hook Entirely
```bash
rm .git/hooks/pre-push
```

**⚠️ Warning**: Bypassing checks can lead to technical debt. Only use in emergencies.

## 📈 Viewing Metrics

### In Pull Requests
Code metrics appear automatically in the PR checks.

### In GitHub Actions
1. Go to **Actions** tab
2. Click on latest **Code Quality Check** run
3. View **Summary** for metrics

### Weekly Reports
Check the automatically created issues labeled `code-quality` for weekly audit results.

## 🛠 Configuration

Edit `.codequality.yml` to adjust limits:

```yaml
standards:
  files:
    max_length: 1000
    warn_length: 500
  classes:
    max_length: 500
    warn_length: 300
```

After changes, commit and the new limits apply immediately.

## 💡 Tips

1. **Start small**: Extract one method at a time
2. **Use descriptive names**: Helps identify responsibilities
3. **Favor composition**: Small classes working together
4. **Test as you refactor**: Ensure behavior doesn't change
5. **Review regularly**: Don't wait for weekly audit

## 🤝 Contributing

When contributing:
1. Run `./scripts/check-code-length.sh` before committing
2. Ensure all new code meets standards
3. Break large features into focused components
4. Document complex refactorings in commit messages

## 📚 Resources

- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Clean Code by Robert C. Martin](https://www.oreilly.com/library/view/clean-code-a/9780136083238/)
- [Code Smells and Refactoring](https://refactoring.guru/refactoring/smells)

---

**Questions?** Open an issue labeled `code-quality` for discussion.


