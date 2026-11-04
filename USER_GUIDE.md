# ifcFireBot — User Guide

A command-line tool for working with IFC building models.  
No installation required — just run the `.exe` file.

---

## Requirements

|	Requirement	      |                    Details                       |
|---------------------|--------------------------------------------------|
| Operating system    | Windows 10 or later (64-bit)                     |
| .NET runtime        | **Not required** — the `.exe` is self-contained  |
| IFC file            | A valid `.ifc` file on your computer             |
| HuggingFace API key | **Only needed for Q&A mode** (see section below) |

---

## How to Start the Program

1. Double-click `ifcFireBot.exe`, or open a terminal and run:
   ```
   .\ifcFireBot.exe
   ```

2. You will be greeted with a mode selection screen:
   ```
   ╔══════════════════════════════════════════════╗
   ║          Welcome to ifcFireBot               ║
   ╚══════════════════════════════════════════════╝

   Please select a mode:

	 [1]    Analyze Mode  — scan IFC file for data quality issues (no API key needed)
	 [2]    Q&A Mode      — ask questions about your IFC model using AI
	 [exit] Exit          — close the program  (also works at any prompt)

   Enter 1, 2, or 'exit':
   ```

---

## Mode 1 — Analyze Mode

**What it does:** Scans your IFC model and reports data quality issues such as:
- Missing names or descriptions on elements
- Duplicate global IDs
- Elements with no geometry
- Missing property sets
- Inconsistencies in building storey assignments

**No API key is required for this mode.**

### Steps

1. Select `1` and press Enter
2. Enter the full path to your `.ifc` file, for example:
   ```
   C:\Projects\MyBuilding\model.ifc
   ```
   - Type `return` to go back to the main menu, or `exit` to close the program
3. The program scans the model and prints a report grouped by severity:
   - 🔴 **ERROR** — critical problems that affect data reliability
   - 🟡 **WARNING** — issues worth reviewing
   - 🔵 **INFO** — general observations
4. When the report is done, you are asked what to do next:
   ```
     [y]      Analyze another file
     [return] Return to main menu
     [exit]   Exit the program

   Enter y, 'return', or 'exit':
   ```
   - Type `y` to analyze another file
   - Type `return` to go back to the main menu
   - Type `exit` to close the program

---

## Mode 2 — Q&A Mode

**What it does:** Lets you ask natural language questions about your IFC model.  
The AI reads the model and answers questions like:
- *"How many rooms are on floor 2?"*
- *"What is the total area of all IfcSpace elements?"*
- *"List all fire doors in the building"*
- *"What is the building height?"*

The AI model is **Meta-Llama 3 (8B)**, accessed through the HuggingFace Inference Router.

### Steps

1. Select `2` and press Enter
2. Enter the full path to your `.ifc` file
   - Type `return` to go back to the main menu, or `exit` to close the program
3. Enter your HuggingFace API key (input is hidden with `*`)
   - Type `return` to go back to the main menu, or `exit` to close the program
4. The model is loaded — you will see:
   ```
   IFC model 'your_file.ifc' opened successfully.
   Model contains XXXX instances of different types.

   Commands: 'return' — main menu  |  'exit' — quit program  |  'debug' — toggle debug mode
   Enter your question about the IFC model:
   ```
5. Type your question and press Enter
6. The AI answers, followed by a **confidence section** if data quality issues were
   detected that may affect the answer
7. After each answer the program shows:
   ```
   ---
   Commands: 'return' — main menu  |  'exit' — quit program  |  'debug' — toggle debug mode
   Ask another question, or enter a command:
   ```
   Type another question, `return` to go back to the main menu, or `exit` to close the program

### Supported commands during Q&A

| Command          | What it does                                           |
|------------------|--------------------------------------------------------|
| *(any question)* | Ask the AI about the IFC model                         |
| `debug`          | Toggle debug mode (shows raw AI intent + element list) |
| `return`         | Return to the main menu                                |
| `exit`           | Close the program                                      |

---

## Providing Your IFC File Path

The program accepts both full absolute paths and relative paths.

**Examples of valid paths:**
```
C:\Users\YourName\Documents\Projects\building.ifc
C:\BIM\Models\OfficeBlock_v3.ifc
.\models\house.ifc
```

**Tips:**
- In Windows Explorer, **right-click** the `.ifc` file → **Copy as path** and paste it directly
- The path must point to a file with a `.ifc` extension
- The file must exist — the program will tell you if it cannot be found and ask you to try again

---

## HuggingFace API Key

### What is it?

A HuggingFace API key (called an *Access Token*) is a private credential that allows
the program to use AI models hosted on HuggingFace. It is **only needed for Q&A mode**.

### How to create one

1. Go to [https://huggingface.co](https://huggingface.co) and create a free account (or log in)
2. Click your profile picture (top right) → **Settings**
3. In the left menu, click **Access Tokens**
4. Click **New token**
5. Give it a name (e.g. `ifcFireBot`) and select **Read** as the role
6. Click **Generate a token**
7. **Copy the token immediately** — it will only be shown once

The token looks like this:
```
hf_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

### How to use it in the program

**First time entering Q&A mode in a session:**
```
Your API key will be used this session only — it is never saved to disk.
  (type 'return' or 'exit' then press Enter to leave this prompt)
Enter your HuggingFace API key: ********************************
```
Paste or type your token. The input is hidden for security.

**Every time you return to Q&A mode in the same session:**
```
API key already entered this session — press Enter to reuse it.
  (type a new key to replace it  |  'return' or 'exit' to leave this prompt)
Enter your HuggingFace API key:
```
Just press **Enter** to reuse the key you already entered. You can also type a new key to replace it for the rest of the session.

> ⚠️ The key is used **only for this session** and is **never saved to disk**.
> When you close the program, the key is gone from memory.

### How to store your key safely (your responsibility)

Since the program does not store the key, you need to keep it somewhere secure yourself.

| Option | Notes |
|---|---|
| **Password manager** (recommended) | e.g. Bitwarden, 1Password, KeePass — most secure |
| **Encrypted notes app** | e.g. Standard Notes |
| **A private text file** | Keep it off shared drives and away from Git repositories |

> ❌ **Never** paste your API key into a shared document, email, Teams message,
> or commit it to a Git repository.

### How to rotate or revoke your key

If you think your key has been compromised:
1. Go to [https://huggingface.co/settings/tokens](https://huggingface.co/settings/tokens)
2. Click the **delete** icon next to your token
3. Generate a new one

Rotating the key immediately invalidates the old one — the program cannot be used
with the old key anymore.

### Free tier limits

HuggingFace provides a free inference tier. Usage limits depend on current HuggingFace
policies. If you receive a rate-limit error, wait a few minutes and try again.

---

## Troubleshooting

| Problem                              | Solution                                                                                        |
|--------------------------------------|-------------------------------------------------------------------------------------------------|
| *"File not found"*                   | Check that the path is correct and the file exists. Try copying the path from Windows Explorer. |
| *"Not a valid .ifc file"*            | Ensure the file extension is `.ifc`.                                                            |
| *"API rate limit or service error"*  | You have exceeded the free HuggingFace rate limit. Wait 1–2 minutes and restart.                |
| *"Network error connecting to API"*  | Check your internet connection.                                                                 |
| *"Could not interpret AI response"*  | The AI returned an unexpected format. Try rephrasing your question in simpler terms.            |
| Program opens and closes immediately | Run from a terminal (`.\ifcFireBot.exe`) so you can read any error messages.                    |

---

## Architecture Overview (for technical readers)

```
ifcFireBot.exe
├── Mode Selection (startup)
│
├── Analyze Mode
│   ├── IFCAnalyzer       — scans all entity types for data quality issues
│   └── ReportGenerator   — prints the issue report to the console
│
└── Q&A Mode
	├── IFCAnalyzer       — silent background scan per query (for confidence indicators)
	├── FetchIfcEnteties  — retrieves relevant IFC elements based on AI intent
	├── Calculate         — computes the answer (count, area, height, list, etc.)
	├── PromptBuilder     — builds the system prompt and parses the AI YAML response
	└── Ai                — connects to HuggingFace Inference Router (Meta-Llama 3 8B)
```

**External dependency:** [HuggingFace Inference Router](https://huggingface.co/docs/api-inference/index)
at `https://router.huggingface.co/v1`

**IFC parsing:** [Xbim Toolkit](https://docs.xbim.net/) (open source, bundled inside the `.exe`)
