-- Class 3 roster and weekly test results, from the printed sheet
-- "টিএসসি টিচার্স স্টুডেন্ট কেয়ার — সাপ্তাহিক ফলাফল (তৃতীয় শ্রেণী)".
--
-- One batch for the class: the morning and afternoon branches sit the same paper and are
-- ranked together, and an Exam belongs to exactly one batch.
--
-- Run:  PGCLIENTENCODING=UTF8 psql -h localhost -U postgres -d coaching_center -f this.sql
-- Runs once: a second run fails on the unique StudentId index and rolls everything back.

BEGIN;

INSERT INTO "Batches" ("Name", "MonthlyFee", "Description")
VALUES ('Class 3', 700,
        'Class 3 — morning and afternoon branches combined; weekly results are ranked together.');

-- The sheet records one paper per week with no subject breakdown, so the marks hang off a
-- single "Weekly Test" subject. If a week was actually Bangla/Maths/etc, repoint that exam's
-- ExamSubject and its marks at the real subject.
INSERT INTO "Subjects" ("Name", "Code")
VALUES ('Weekly Test', 'WKLY');

INSERT INTO "Exams" ("Name", "ExamDate", "BatchId")
SELECT e.name, e.on_date, b."Id"
FROM (VALUES
    ('Weekly Test 1 — 24 Jan 2026', DATE '2026-01-24'),
    ('Weekly Test 2 — 31 Jan 2026', DATE '2026-01-31'),
    ('Weekly Test 3 — 07 Feb 2026', DATE '2026-02-07'),
    ('Weekly Test 4 — 21 Feb 2026', DATE '2026-02-21'),
    ('Weekly Test 5 — 28 Feb 2026', DATE '2026-02-28')
) AS e(name, on_date)
CROSS JOIN (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 3') b;

-- Full marks differ week to week: 40 / 50 / 30 / 50 / 40, 210 in total.
INSERT INTO "ExamSubjects" ("ExamId", "SubjectId", "FullMarks")
SELECT x."Id", s."Id", f.full_marks
FROM (VALUES
    (DATE '2026-01-24', 40),
    (DATE '2026-01-31', 50),
    (DATE '2026-02-07', 30),
    (DATE '2026-02-21', 50),
    (DATE '2026-02-28', 40)
) AS f(on_date, full_marks)
JOIN "Exams" x
  ON x."ExamDate" = f.on_date
 AND x."BatchId" = (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 3')
CROSS JOIN (SELECT "Id" FROM "Subjects" WHERE "Code" = 'WKLY') s;

-- Roll number -> Student ID: roll 01 is TSC0001. Scores are in sheet order; NULL is a blank
-- cell on the sheet (absent), which becomes no mark row rather than a zero.
CREATE TEMP TABLE sheet (student_id text, full_name text, scores numeric[]) ON COMMIT DROP;

INSERT INTO sheet VALUES
('TSC0001', 'মিফতাহুল রাইসা',          ARRAY[40,   48,   28, 49,   40  ]),
('TSC0002', 'ফাইহান ইসলাম',            ARRAY[40,   NULL, 28, 47,   40  ]),
('TSC0003', 'আবদুল্লাহ আল কাফি',        ARRAY[39,   38,   27, 49,   40  ]),
('TSC0004', 'পৃথী সরকার',               ARRAY[34,   44,   29, NULL, 39  ]),
('TSC0005', 'রামিজা রাফা',              ARRAY[26,   NULL, 28, 44,   38  ]),
('TSC0006', 'ফাইয়াজ আবরার',            ARRAY[10,   25.5, NULL, 34, NULL]),
('TSC0007', 'সানিয়া আহমেদ',            ARRAY[30,   42,   28, 47,   38  ]),
('TSC0008', 'আদ ইফা জান্নাত জারা',      ARRAY[7,    32,   26, 46,   34  ]),
('TSC0009', 'ফারহান সাদিক আবির',       ARRAY[33,   42,   27, NULL, 38  ]),
('TSC0010', 'সাদিক হাসান',              ARRAY[34,   NULL, 25, 40,   34  ]),
('TSC0011', 'সাদমান তাহমিদ তুর্য',       ARRAY[39,   NULL, 28, 49,   38  ]),
('TSC0012', 'যায়েদ বিন আতিক',          ARRAY[28,   43,   26, 45.5, 19  ]),
('TSC0013', 'সাদমান সাকিব',             ARRAY[NULL, 42,   NULL, 38, 29  ]),
('TSC0014', 'তাসনিম জাহান',             ARRAY[28,   38,   27, 46,   NULL]),
('TSC0015', 'ফারিহা ইসলাম ইথিকা',       ARRAY[7,    NULL, 17, 31,   26  ]),
('TSC0016', 'আহনাফ শাহরিয়ার নিবির',     ARRAY[26.5, 26,   28, 44.5, 27  ]),
('TSC0017', 'আল মিজান আদিব',           ARRAY[33,   46,   28, 49,   40  ]),
('TSC0018', 'মোঃ ত্বহা মাহমুদ',          ARRAY[20,   29,   25, 37,   35  ]),
('TSC0019', 'মাশিয়াত ইসলাম আলিফ',      ARRAY[21,   22,   26, 41,   16  ]),
('TSC0020', 'রাহা',                      ARRAY[NULL, NULL, 27, 47,   NULL]),
('TSC0021', 'মোছাঃ আয়াত আল হাসান',    ARRAY[27,   34,   26, 35,   37  ]),
('TSC0022', 'মোছাঃ রুবা মনি',            ARRAY[29.5, 42,   27, NULL, 36  ]),
('TSC0023', 'আওসাব',                    ARRAY[20,   30,   24, 42,   23  ]),
('TSC0024', 'মুশফিকুর রহিম',            ARRAY[35,   37,   26, 46,   30  ]),
('TSC0025', 'ইরফান রহমান রাফি',         ARRAY[39,   40,   27, 47.5, 32  ]),
('TSC0026', 'তৌফিফুল ইসলাম জিসান',      ARRAY[29,   37,   27, 46,   34  ]),
('TSC0027', 'আয়ুসি দেবনাথ',             ARRAY[24,   37,   28, 42.5, 38  ]),
('TSC0028', 'ইশরাত জামান নুহা',          ARRAY[27,   46,   28, 49,   NULL]),
('TSC0029', 'আসিফ আলহাবিব সৌহার্দ্য',   ARRAY[26.5, NULL, 23, 44,   20  ]),
('TSC0030', 'নাফিজুল হক আলিফ',          ARRAY[27,   32,   NULL, 39, NULL]),
('TSC0031', 'আসনিম আলম ফারিহা',        ARRAY[31,   45,   26, 45.5, 39  ]),
('TSC0032', 'আব্দুল্লাহ আল আয়ান',        ARRAY[NULL, 32,   26, 38,   17  ]),
('TSC0033', 'মুশফিকা নাজনীন (মিথি)',     ARRAY[36,   NULL, 28, 48,   40  ]),
('TSC0034', 'অরিত্র মহন্ত',              ARRAY[19,   17,   25, NULL, 16  ]),
('TSC0035', 'দেবব্রত সাহা প্রিয়ম',       ARRAY[17,   21,   20, 32,   19  ]),
('TSC0036', 'অয়ন্তিকা সাহা',             ARRAY[NULL, 37,   28, 49,   39  ]);

INSERT INTO "Students" ("StudentId", "FullName", "ClassLevel", "BatchId", "IsActive")
SELECT sheet.student_id, sheet.full_name, '3', b."Id", true
FROM sheet
CROSS JOIN (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 3') b;

INSERT INTO "Marks" ("StudentId", "ExamId", "SubjectId", "Score")
SELECT st."Id", x."Id", s."Id", cell.score
FROM sheet
JOIN "Students" st ON st."StudentId" = sheet.student_id
CROSS JOIN LATERAL unnest(sheet.scores) WITH ORDINALITY AS cell(score, week)
JOIN (VALUES
    (1, DATE '2026-01-24'),
    (2, DATE '2026-01-31'),
    (3, DATE '2026-02-07'),
    (4, DATE '2026-02-21'),
    (5, DATE '2026-02-28')
) AS w(week, on_date) ON w.week = cell.week
JOIN "Exams" x
  ON x."ExamDate" = w.on_date
 AND x."BatchId" = (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 3')
CROSS JOIN (SELECT "Id" FROM "Subjects" WHERE "Code" = 'WKLY') s
WHERE cell.score IS NOT NULL;

COMMIT;
