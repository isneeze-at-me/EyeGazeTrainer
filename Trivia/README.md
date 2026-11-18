# Level 1 True/False Trivia — 500-question bank

**Status:** Question bank and Level 1 ten-question bonus flow implemented on `Test`; Windows/UWP compilation and supervised gaze hardware testing remain **unverified**.

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

## Implemented quiz behavior

- Following Level 1, the three-second celebration automatically opens a ten-question True/False round.
- The complete statement appears centered in the top half. Two large True and False buttons fill the bottom half.
- The existing shared 1500 ms gaze-button dwell, radial progress wheel, global smoothing and adaptive button hitbox are reused. Mouse or touch Click works for developer tests.
- Ten questions are selected randomly across available topics, with a preference for different topics and balanced truth values. No two questions from the same fact group appear in one round.
- Dealt question IDs are saved to UWP LocalSettings across sessions; none repeats until the pool of 500 is exhausted. At exhaustion the deck replenishes.
- After an answer, the answer and explanation replace the statement for five seconds, regardless of correctness. Timing pauses if live gaze samples stop arriving; preview mode proceeds normally.
- The separate trivia score is the count correct. Incorrect answers leave the existing gaze accuracy score untouched; correct answers from a valid live-gaze dwell can update it using the measured button selection. Mouse clicks do not impersonate gaze accuracy measurements.
- After the tenth explanation, show the result for three seconds and return to the level-selection screen (also paused if the live gaze stream is lost).
- Quiz timing never calls the app's gaze cleanup or exit routine. The prior live-gaze hardware recovery warning still applies.

## Verification pending

Run Visual Studio UWP x64 build, confirm all five JSON content files are included in the deployment package, test a complete round with mouse, verify persisted non-repeating deck over restarts, test late-deck cycle reset, and separately validate button accuracy with **supervised** eye-gaze testing only after critical TD Control recovery issue #9 has been resolved.

Editorial QA: record counts, category counts, unique IDs and question strings were checked programmatically when added. Factual statements have not undergone independent external fact-checking or user acceptance testing.
