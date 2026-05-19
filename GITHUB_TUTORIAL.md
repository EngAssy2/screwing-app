# 🐙 GitHub Tutorial for Developers
### Spare Part Inventory Management System

> **Target Audience:** Developers joining the project for the first time, or engineers who need a refresher on the Git workflow used in this repository.

---

## 📋 Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [First-Time Setup](#2-first-time-setup)
3. [Understanding the Repository Structure](#3-understanding-the-repository-structure)
4. [Branching Strategy](#4-branching-strategy)
5. [Daily Development Workflow](#5-daily-development-workflow)
6. [Writing Good Commit Messages](#6-writing-good-commit-messages)
7. [Creating a Pull Request (PR)](#7-creating-a-pull-request-pr)
8. [Resolving Merge Conflicts](#8-resolving-merge-conflicts)
9. [Keeping Your Branch Up to Date](#9-keeping-your-branch-up-to-date)
10. [Tagging a Release](#10-tagging-a-release)
11. [Useful Git Commands Cheatsheet](#11-useful-git-commands-cheatsheet)
12. [What NOT to Commit](#12-what-not-to-commit)
13. [Team Rules & Etiquette](#13-team-rules--etiquette)

---

## 1. Prerequisites

Make sure you have these installed before you begin:

| Tool | Version | Download |
|---|---|---|
| Git | 2.40+ | https://git-scm.com/downloads |
| Node.js | 18+ | https://nodejs.org |
| VS Code (recommended) | Latest | https://code.visualstudio.com |

**Verify Git is installed:**
```bash
git --version
# Should output: git version 2.x.x
```

**Configure your identity (one-time setup):**
```bash
git config --global user.name "Your Full Name"
git config --global user.email "your.email@company.com"
```

> [!TIP]
> Use your company email address — it will appear in all your commits.

---

## 2. First-Time Setup

### Step 1 — Clone the Repository

```bash
git clone https://github.com/YOUR_ORG/sparepart-app.git
cd sparepart-app
```

### Step 2 — Install Dependencies

This is a **monorepo** — it has two separate projects: `backend` and `frontend`. You must install dependencies for both.

```bash
# Install backend dependencies
cd backend
npm install

# Install frontend dependencies
cd ../frontend
npm install
```

### Step 3 — Set Up Environment Variables

The `.env` file is **NOT committed to Git** (it's in `.gitignore`). You must create it manually.

Create a file called `.env` inside `/backend/` with this structure:

```ini
# Database Configuration
DB_HOST=localhost
DB_PORT=3306
DB_USER=YOUR_DB_USER
DB_PASSWORD=YOUR_DB_PASSWORD
DB_NAME=sparepartinventorydb

# JWT
JWT_SECRET=your_secret_key_here
JWT_EXPIRES_IN=8h

# Central Auth API
CENTRAL_AUTH_BASE_URL=http://localhost:3001/api/v1
CENTRAL_AUTH_API_KEY=your_api_key_here
CENTRAL_ADMIN_BADGE=your_admin_badge
CENTRAL_ADMIN_PASSWORD=your_admin_password

# Server
PORT=5050

# File Storage (path to uploaded images/datasheets)
UPLOAD_DIR=C:\uploads

# Environment
NODE_ENV=development
```

> [!CAUTION]
> **NEVER commit your `.env` file to GitHub.** It contains passwords and secret keys. The `.gitignore` already blocks it, but always double-check before pushing.

### Step 4 — Start the Development Servers

**Terminal 1 — Backend:**
```bash
cd backend
npm run dev
# Server starts on http://localhost:5050
```

**Terminal 2 — Frontend:**
```bash
cd frontend
npm run dev
# App opens on http://localhost:5173
```

---

## 3. Understanding the Repository Structure

```
sparepart-app/                  ← Root of the Git repository
│
├── .git/                       ← Git tracking folder (never touch this)
├── .gitignore                  ← Files excluded from Git tracking
├── README.md                   ← Project overview
├── GITHUB_TUTORIAL.md          ← This file
├── api_guide.md                ← API documentation
│
├── backend/                    ← Node.js / Express API server
│   ├── middleware/
│   │   ├── auth.js             ← JWT authentication middleware
│   │   └── sanitize.js         ← Input trimming middleware (global)
│   ├── routes/                 ← All API route handlers
│   │   ├── auth.js             ← Login / change-password
│   │   ├── parts.js            ← Parts CRUD
│   │   ├── transactions.js     ← Stock In / Stock Out
│   │   ├── users.js            ← User management
│   │   ├── procurement.js      ← PR / PO / DO workflow
│   │   ├── stockout-public.js  ← Public (no-login) stock out
│   │   ├── files.js            ← File upload/download
│   │   ├── reports.js          ← Reports & Excel export
│   │   └── notifications.js    ← Notification system
│   ├── config/
│   │   └── db.js               ← MySQL connection pool
│   ├── server.js               ← Express app entry point
│   ├── .env                    ← NOT in Git – create manually
│   └── package.json
│
└── frontend/                   ← React 19 + Vite application
    ├── src/
    │   ├── api/                ← Axios client setup
    │   ├── components/         ← Reusable UI components
    │   │   └── layout/         ← AppShell, Sidebar, etc.
    │   ├── context/            ← React Context (Auth)
    │   ├── pages/              ← One file per page/feature
    │   │   ├── parts/          ← Part list, form, detail
    │   │   ├── stock/          ← Stock In, Stock Out, Public pages
    │   │   ├── transactions/   ← Transaction history
    │   │   ├── procurement/    ← PR, PO, DO pages
    │   │   ├── users/          ← User management
    │   │   └── reports/        ← Reports page
    │   └── index.css           ← Global styles (design system)
    ├── vite.config.js          ← Dev proxy config (points to backend)
    └── package.json
```

---

## 4. Branching Strategy

We follow a simplified **GitHub Flow** branching model.

```
main ──────────────────────────────────────────────────► (production)
  │
  ├── feature/add-low-stock-alert
  ├── feature/export-pdf-report
  ├── fix/part-number-whitespace-bug
  └── hotfix/stock-out-calculation-error
```

### Branch Naming Rules

| Type | Format | Example |
|---|---|---|
| New feature | `feature/short-description` | `feature/add-qr-scanner` |
| Bug fix | `fix/short-description` | `fix/login-blank-password` |
| Urgent fix on prod | `hotfix/short-description` | `hotfix/stock-negative-qty` |
| Refactor / cleanup | `refactor/short-description` | `refactor/parts-api-cleanup` |

> [!IMPORTANT]
> **Never commit directly to `main`.** Always work on a separate branch and create a Pull Request.

---

## 5. Daily Development Workflow

Follow these steps every time you start working on a task:

### Step 1 — Pull Latest Changes from Main

Before creating a new branch, always sync with the latest code:

```bash
git checkout main
git pull origin main
```

### Step 2 — Create a New Branch

```bash
git checkout -b feature/your-feature-name

# Example:
git checkout -b fix/trim-input-whitespace
```

### Step 3 — Make Your Code Changes

Edit files in VS Code. Use `npm run dev` in both `backend/` and `frontend/` to test your changes live.

### Step 4 — Check What Files You Changed

```bash
git status
```

This shows:
- Green = Staged (ready to commit)
- Red = Modified but not staged yet

### Step 5 — Stage Your Changes

```bash
# Stage specific files (recommended — be intentional)
git add backend/middleware/sanitize.js
git add frontend/src/pages/parts/PartFormPage.jsx

# OR stage everything in a folder
git add backend/

# OR stage ALL changed files (use with caution)
git add .
```

### Step 6 — Commit Your Changes

```bash
git commit -m "fix: trim leading/trailing whitespace in all form inputs"
```

### Step 7 — Push Your Branch to GitHub

```bash
git push origin feature/your-feature-name

# If this is your first push for this branch:
git push -u origin feature/your-feature-name
```

### Step 8 — Create a Pull Request

Go to GitHub → Your repository → you will see a banner **"Compare & pull request"** → Click it.

---

## 6. Writing Good Commit Messages

We use the **Conventional Commits** format:

```
<type>(<scope>): <short description>

[optional body — more details]
```

### Types

| Type | When to use |
|---|---|
| `feat` | Adding a new feature |
| `fix` | Fixing a bug |
| `refactor` | Code cleanup without changing behaviour |
| `docs` | Documentation changes only |
| `chore` | Build scripts, configs, dependency updates |
| `style` | CSS / UI changes, no logic |

### Examples

```bash
# Good
git commit -m "feat(parts): add duplicate part number check on blur"
git commit -m "fix(stock-out): trim Part_Number before DB lookup"
git commit -m "refactor(backend): add global sanitize middleware"
git commit -m "docs: add GitHub workflow tutorial"

# Bad
git commit -m "fix bug"
git commit -m "update"
git commit -m "changes"
```

> [!TIP]
> A good commit message completes the sentence: **"If applied, this commit will…"**
> - "…fix the trim whitespace bug on Part Number" ← Good
> - "…stuff" ← Bad

---

## 7. Creating a Pull Request (PR)

### On GitHub

1. Go to the repository on GitHub
2. Click **"Pull requests"** tab → **"New pull request"**
3. Set **base:** `main` ← **compare:** `feature/your-branch`
4. Fill out the PR description:

```markdown
## What does this PR do?
Brief description of the change.

## Why?
Explain the reason / which issue this fixes.

## How to test?
1. Start both backend and frontend
2. Navigate to the Register Part page
3. Try typing a space before/after the Part Number
4. Submit — verify no error occurs

## Checklist
- [ ] I tested this locally
- [ ] I did not commit `.env` or `node_modules`
- [ ] The backend server still starts with `npm run dev`
- [ ] The frontend still builds with `npm run dev`
```

5. Click **"Create pull request"**
6. Assign a **Reviewer**

> [!NOTE]
> PRs require at least **1 approval** before merging into `main`.

---

## 8. Resolving Merge Conflicts

A **merge conflict** happens when two people edited the same part of the same file.

### What a Conflict Looks Like in a File

```javascript
<<<<<<< HEAD  (your current branch)
const Part_Number = req.body.Part_Number.trim();
=======        (incoming change from main)
const Part_Number = req.body.Part_Number;
>>>>>>> main
```

### How to Fix It

1. Open the conflicted file in VS Code
2. VS Code highlights the conflict with buttons: **Accept Current Change** | **Accept Incoming Change** | **Accept Both**
3. Choose the correct version, delete the `<<<<<<<`, `=======`, `>>>>>>>` markers
4. Save the file
5. Mark it as resolved and commit:

```bash
git add backend/routes/parts.js
git commit -m "fix: resolve merge conflict in parts route"
```

---

## 9. Keeping Your Branch Up to Date

If your feature branch has been open for a while, `main` may have new commits. Always merge `main` into your branch before creating a PR:

```bash
# Make sure you are on your feature branch
git checkout feature/your-feature-name

# Fetch latest changes from remote
git fetch origin

# Merge main into your branch
git merge origin/main

# If there are conflicts, resolve them (see Section 8)

# Push the updated branch
git push origin feature/your-feature-name
```

> [!WARNING]
> Do **NOT** use `git rebase` on branches that are already pushed to GitHub unless you know what you're doing — it rewrites history and can cause problems for teammates.

---

## 10. Tagging a Release

When a version is ready for production deployment, create a tag:

```bash
# Make sure you are on main and it's up to date
git checkout main
git pull origin main

# Create an annotated tag
git tag -a v1.2.0 -m "Release v1.2.0 — Input sanitization, procurement flow"

# Push the tag to GitHub
git push origin v1.2.0
```

### Version Numbering (Semantic Versioning)

| Increment | When |
|---|---|
| `PATCH` (1.0.**1**) | Bug fixes, small corrections |
| `MINOR` (1.**1**.0) | New features, backward-compatible |
| `MAJOR` (**2**.0.0) | Breaking changes, full redesign |

---

## 11. Useful Git Commands Cheatsheet

```bash
# STATUS & INSPECTION
git status                         # See what's changed
git log --oneline -10              # Last 10 commits (short)
git diff                           # See unstaged changes
git diff --staged                  # See staged changes

# BRANCH MANAGEMENT
git branch                         # List local branches
git branch -a                      # List all branches (incl. remote)
git checkout -b feature/xyz        # Create and switch to new branch
git checkout main                  # Switch back to main
git branch -d feature/xyz          # Delete local branch (after merge)

# SYNC WITH REMOTE
git fetch origin                   # Download remote changes (no merge)
git pull origin main               # Fetch + merge main
git push origin feature/xyz        # Push your branch to GitHub

# UNDO MISTAKES
git restore <file>                 # Discard changes in a file
git restore --staged <file>        # Unstage a file (keep changes)
git reset --soft HEAD~1            # Undo last commit (keep changes)
git stash                          # Temporarily save uncommitted work
git stash pop                      # Restore stashed work
```

---

## 12. What NOT to Commit

The `.gitignore` file already blocks most of these, but always be aware:

| Never Commit | Why |
|---|---|
| `backend/.env` | Contains passwords, API keys, DB credentials |
| `node_modules/` | Huge, auto-generated — run `npm install` instead |
| `frontend/dist/` | Build output — generated by `npm run build` |
| `*.log` | Log files — machine-specific noise |
| Upload folder contents | User data, binary files |

**Before every `git push`, verify with:**
```bash
git diff --staged --name-only
```
Make sure you don't see `.env` or `node_modules` in the list.

---

## 13. Team Rules & Etiquette

### DO

- Pull from `main` before starting any new work
- Use meaningful branch names (`fix/`, `feature/`, `hotfix/`)
- Write clear commit messages (Conventional Commits format)
- Test your changes locally before pushing
- Assign a reviewer on every PR
- Delete your feature branch after it is merged

### DON'T

- Push directly to `main`
- Leave a PR open for more than 3 days without activity
- Merge your own PR without a review (unless urgent hotfix)
- Commit secrets, passwords, or `.env` files
- Force-push (`git push --force`) to shared branches

---

## Common Issues & Fixes

### "I accidentally committed to main"

```bash
# Undo the commit (keeps changes in your working directory)
git reset --soft HEAD~1

# Create a proper branch and push there instead
git checkout -b fix/accidental-main-commit
git push origin fix/accidental-main-commit
```

### "I committed my .env file by mistake"

```bash
# Remove from tracking (keep the file on disk)
git rm --cached backend/.env

# Make sure .gitignore has .env
echo ".env" >> .gitignore

git add .gitignore
git commit -m "chore: remove .env from tracking"
git push
```

> [!CAUTION]
> If the `.env` with real credentials was already pushed to GitHub, **immediately rotate all passwords and API keys** — the history is permanent.

### "My branch is too far behind main"

```bash
git checkout your-branch
git fetch origin
git merge origin/main
# Resolve any conflicts, then:
git push origin your-branch
```

### "I want to undo all my uncommitted changes"

```bash
# Warning: This is destructive — all unsaved work is lost
git restore .
```

---

*Tutorial maintained by the Engineering Assy 2 team. Last updated: May 2026.*
