-- Class 2, 1st term model test: "১ম সাময়িক মডেল টেস্ট ফলাফল (দ্বিতীয় শ্রেণী)".
-- Seven papers: বাংলা / ইংরেজি / গণিত / ধর্ম / পরি / সাধারণ জ্ঞান / কম্পিউটার,
-- marked out of 80 / 80 / 80 / 80 / 80 / 25 / 25 = 450.
--
-- Student IDs are TSC02NN, NN being the roll on the sheet, because class 3 already owns
-- TSC0001-TSC0064 and rolls repeat from 1 in every class.
--
-- Roll 04 and rolls 26-30 are blank on the sheet and are not imported.
-- The sheet carries no exam date; 1 March 2026 is a placeholder -- correct it if it matters.
--
-- Run:  PGCLIENTENCODING=UTF8 psql -h localhost -U postgres -d coaching_center -f this.sql

BEGIN;

INSERT INTO "Batches" ("Name", "MonthlyFee", "Description")
VALUES ('Class 2', 700, 'Class 2 — morning and afternoon branches combined.');

-- "পরি" is read as পরিবেশ পরিচিতি and mapped onto the existing Science subject.
INSERT INTO "Subjects" ("Name", "Code") VALUES
    ('General Knowledge', 'GK'),
    ('Computer', 'COMP');

INSERT INTO "Exams" ("Name", "ExamDate", "BatchId")
SELECT '1st Model Test — Class 2', DATE '2026-03-01', "Id"
FROM "Batches" WHERE "Name" = 'Class 2';

INSERT INTO "ExamSubjects" ("ExamId", "SubjectId", "FullMarks")
SELECT x."Id", sub."Id", p.full_marks
FROM (VALUES
    ('Bangla',            80),
    ('English',           80),
    ('Mathematics',       80),
    ('Religion',          80),
    ('Science',           80),
    ('General Knowledge', 25),
    ('Computer',          25)
) AS p(subject_name, full_marks)
JOIN "Subjects" sub ON sub."Name" = p.subject_name
CROSS JOIN (SELECT "Id" FROM "Exams" WHERE "Name" = '1st Model Test — Class 2') x;

-- Scores in sheet column order; NULL is a blank cell (did not sit that paper).
CREATE TEMP TABLE sheet (student_id text, full_name text, scores numeric[]) ON COMMIT DROP;

INSERT INTO sheet VALUES
('TSC0201', 'মোছাঃ আনায়া',            ARRAY[75,   73,   73,   80,   73,   22,   25  ]),
('TSC0202', 'হামিম আহমেদ',             ARRAY[43,   46,   70,   52,   54,   21,   6   ]),
('TSC0203', 'নুসরাত জাহান',            ARRAY[79,   69,   73,   80,   80,   24,   23  ]),
('TSC0205', 'অধরা ইসলাম',              ARRAY[69,   48,   71,   66,   61,   22,   22  ]),
('TSC0206', 'আবু তাহের',                ARRAY[55,   33,   53,   51,   53,   23,   9   ]),
('TSC0207', 'মাহির ফয়সাল',             ARRAY[73,   75,   80,   75,   77,   24,   22  ]),
('TSC0208', 'মুবাশ্বির আল মুবিন',       ARRAY[72,   66,   77,   74,   71,   24,   16  ]),
('TSC0209', 'জারিফ রহমান',             ARRAY[73,   72,   79,   76,   73,   22,   23  ]),
('TSC0210', 'ইমাম হাসান',               ARRAY[54,   NULL, NULL, 54,   NULL, 20,   7   ]),
('TSC0211', 'অংকুশ',                     ARRAY[77,   71,   80,   77,   78,   25,   25  ]),
('TSC0212', 'তাসফিয়া আক্তার তুবা',      ARRAY[65,   52,   78,   52,   74,   21,   15  ]),
('TSC0213', 'মোছাদ্দেক ইসলাম সাদ',      ARRAY[75,   61,   72,   74,   70,   23,   20  ]),
('TSC0214', 'জান্নাতুল রওয়া',           ARRAY[72,   70,   74,   77,   72,   25,   25  ]),
('TSC0215', 'আহনাফ হাসান ইহান',        ARRAY[80,   74,   78,   78,   73,   25,   24  ]),
('TSC0216', 'মারিয়াম মেহজাবিন',        ARRAY[NULL, 29,   NULL, NULL, NULL, NULL, NULL]),
('TSC0217', 'উম্মে আয়মান',              ARRAY[78,   67,   78,   80,   74,   25,   24  ]),
('TSC0218', 'শার্লিন জারা',              ARRAY[56,   40,   47,   54,   65,   22,   11  ]),
('TSC0219', 'নাজাত',                      ARRAY[56,   44,   63,   NULL, 48,   NULL, NULL]),
('TSC0220', 'তনায়া',                     ARRAY[62,   61,   NULL, NULL, 48,   NULL, NULL]),
('TSC0221', 'মুবাশ্বিরা',                 ARRAY[61,   60,   69,   68,   69,   24,   23  ]),
('TSC0222', 'সালসাবিল ইসলাম',           ARRAY[73,   68,   75,   78,   77,   25,   21  ]),
('TSC0223', 'জান্নাতুল মাওয়া ওহী',      ARRAY[41,   33,   35,   57,   49,   13,   4   ]),
('TSC0224', 'সূর্য সাহা',                 ARRAY[77,   76,   78,   76,   76,   25,   25  ]),
('TSC0225', 'মারিয়াম মেহজাবিন',        ARRAY[NULL, NULL, 52,   NULL, NULL, 22,   9   ]);

INSERT INTO "Students" ("StudentId", "FullName", "ClassLevel", "BatchId", "IsActive")
SELECT sheet.student_id, sheet.full_name, '2', b."Id", true
FROM sheet
CROSS JOIN (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 2') b;

INSERT INTO "Marks" ("StudentId", "ExamId", "SubjectId", "Score")
SELECT st."Id", x."Id", sub."Id", cell.score
FROM sheet
JOIN "Students" st ON st."StudentId" = sheet.student_id
CROSS JOIN LATERAL unnest(sheet.scores) WITH ORDINALITY AS cell(score, position)
JOIN (VALUES
    (1, 'Bangla'),
    (2, 'English'),
    (3, 'Mathematics'),
    (4, 'Religion'),
    (5, 'Science'),
    (6, 'General Knowledge'),
    (7, 'Computer')
) AS p(position, subject_name) ON p.position = cell.position
JOIN "Subjects" sub ON sub."Name" = p.subject_name
CROSS JOIN (SELECT "Id" FROM "Exams" WHERE "Name" = '1st Model Test — Class 2') x
WHERE cell.score IS NOT NULL;

COMMIT;
