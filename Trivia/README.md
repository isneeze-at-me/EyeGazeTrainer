# Level 1 True/False Trivia — 500-question bank

**Status:** Question content added to the `Test` branch. The bonus-round screen is currently a placeholder; the quiz selection, answer controls, scoring, and completion behavior are **not implemented yet**.

## Files

- `true-false-001-100.json`
- `true-false-101-200.json`
- `true-false-201-300.json`
- `true-false-301-400.json`
- `true-false-401-500.json`

Each UTF-8 JSON file is an array of 100 records with the fields:

- `id`: stable identifier (`TF001`–`TF500`).
- `category`: topic (25 categories with 20 questions each).
- `question`: a complete statement for the player to judge.
- `answer`: JSON boolean (true or false).
- `explanation`: short, child-appropriate feedback.
- `factGroup`: stable identifier (`F001`–`F250`) shared by the True and False versions of the same underlying fact.

## Content constraints

The bank contains 500 unique statements, evenly split into 250 True and 250 False answers, with simple-to-moderate topics and no intentional political, religious, violent, sexual, or otherwise controversial material. Each category contains 10 True and 10 False answers.

## Future quiz integration

- Shuffle across categories and rotate material to avoid overly repetitive rounds.
- **Never show both questions with the same `factGroup` in one quiz.** Doing so would repeat the underlying fact or reveal an answer.
- Balance True and False answers in each round where practical, rather than displaying the bank in file order.
- Keep the first bonus game compatible with the prior taught gaze skills; avoid requiring novel click, dwell, or navigation interactions without instruction.
- Verify the quiz UI and actual runtime loading before claiming the bonus round is functional.

Editorial QA: record counts, category counts, unique IDs and question strings were checked programmatically when added. Factual statements have not undergone independent external fact-checking or user acceptance testing.
