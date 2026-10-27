# DotNet Interviewer

A local interview practice app for C#/.NET, SQL, architecture and operations. The bank contains 322 exercises: 48 C# tasks, 42 SQL tasks, 164 theory questions and 68 written design/review exercises.

## Start

With Docker Engine running, create the images once:

```sh
docker compose up --build -d
```

Open [localhost:8080](http://localhost:8080) and choose **English** in the header. Later starts can use `docker compose up -d --no-build`. Application source, content and harness files are mounted from the checkout; ordinary edits do not require image rebuilds. Changes to Dockerfiles or system dependencies still do.

## Practice

- **Practice:** C# and SQL tasks with executable checks, code highlighting, indentation controls and reset to starter code.
- **Theory:** topic-based tests, previous/next navigation and single- or multiple-choice answers.
- **Architecture & review:** written scenarios with self-assessment rubrics. Their text is not automatically graded for architectural correctness.
- **Library:** documentation, books and topic-to-exercise links.
- **History:** submitted solutions, results and time. A successful preview is recorded only after submission.

Use the tag filter to focus on Fintech, E-commerce, Security, Distributed systems or another domain/skill. Tags are shared across languages.

Start an attempt to enable editing/answering. The server owns the deadline; changing pages or languages does not reset it. Language switching saves the current answer first. Question IDs and answer positions stay the same, so progress is shared. Export progress before destructive volume operations. `docker compose down` preserves volumes; `docker compose down -v` deletes them.

## English content

The UI, statements, choices, hints, explanations, library and standard result messages have local English translations. No external translation service is used while the app runs. Translations were prepared automatically and selectively edited for technical terminology. User submissions and history were not sent for translation.

Source code and drafts remain verbatim. Starter comments have English translations; signatures, literals and grading stay unchanged. Arbitrary user exception messages and compiler output are shown as emitted.

C# runs on .NET 10. Task-specific pinned NuGet packages are restored into a persistent cache before offline execution. PostgreSQL and SQLite have executable tasks. MongoDB/Cypher/CQL/cloud cases are written exercises unless their statement explicitly says otherwise; those services are not provisioned automatically.

This is a local, single-user tool. The API uses the host Docker socket to launch isolated runners. Keep the app bound to localhost. Runtime instructions and extension details are available in [the main README](README.md) and [content documentation](docs/CONTENT_GUIDE.md).

No application build or tests were run while preparing the language/tag changes, as requested. Validation covered syntax, metadata, translation coverage and unchanged starter code outside comments.

### AI mock interviews

**AI · Mock interviews** supports resume/narrative profiles, screening, technical, lead and final rounds, candidate questions and saved debriefs. Automatic replies and code reviews require an installed, signed-in host Codex CLI. Run `python3 tools/ai_bridge.py`, paste the token in the interview settings and explicitly allow data transfer. Open the trainer at `http://localhost:8080`; for `127.0.0.1` pass `--origin http://127.0.0.1:8080`. Other assistants work through private JSON packets. Browser dictation is optional and may use the browser provider's cloud. PDF resumes are extracted locally in the browser using bundled PDF.js, without a token or host dependencies. Limits: 2 MB, 30 pages and 50,000 characters. Image-only scans need OCR first; review the extracted text before saving. TXT/MD/DOCX import is also supported. [Details](docs/AI_INTERVIEWS.md).

### Automatic interview builder

**Конструктор / Builder** creates persistent plans from the existing bank by topic, tags, time budget and task counts. Choose balanced, gap-focused or random selection. Review actual counts before starting: a limited budget may produce a partial plan. Take the plan independently with an overall server deadline and existing task checks, or create a separate assistant round using exactly that bank. Written self-assessment and assisted attempts are reported separately. [Details](docs/INTERVIEW_BUILDER.md).

### Native ChatGPT Voice

Load **tools/chatgpt-companion** as an unpacked Chrome/Edge extension. Pair one interview with one ChatGPT tab, explicitly share context and optionally sync its visible transcript. Voice runs in ChatGPT; Codex CLI performs code reviews and the saved debrief. No Realtime API key is required. UI automation can break when ChatGPT changes: a manual scenario/transcript workflow is included. Tool-enabled Codex/Work voice tasks can also use **tools/interview_session.py**. [Setup and limits](docs/CHATGPT_VOICE.md).

The ChatGPT companion now discovers the open interview through localhost, including Safari → Chrome. Approve its request on the interview page; the token transfers automatically when you return to the companion. Manual token entry remains a fallback. The Codex bridge token still comes from the terminal.

AI rounds now start in preparation mode without consuming interview time. Connect the assistant first, then click **Start interview** on the round page or in the companion to start the persistent server deadline. Repeated start requests do not reset it.
