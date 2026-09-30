-- Class 3 weekly results, page 2 of the printed sheet: rolls 38-64 (the second branch).
-- Same five papers and same full marks as page 1, so these rows join the existing exams and
-- the existing "Class 3" batch -- that is what makes the combined ranking work.
--
-- Roll 37 and roll 65 are blank on the sheet and are not imported.
-- Roll 64 (মুহাইমিনুল) sat none of the five papers: student added, no marks.
--
-- Run:  PGCLIENTENCODING=UTF8 psql -h localhost -U postgres -d coaching_center -f this.sql

BEGIN;

CREATE TEMP TABLE sheet2 (student_id text, full_name text, scores numeric[]) ON COMMIT DROP;

INSERT INTO sheet2 VALUES
('TSC0038', 'জে এন সাবিহা ফারজানা',    ARRAY[32,   43,   28,   46,   37  ]),
('TSC0039', 'হাসান মোস্তাক',            ARRAY[NULL, 25,   NULL, 31,   NULL]),
('TSC0040', 'তাসফিহা',                  ARRAY[5,    NULL, 19,   36,   25  ]),
('TSC0041', 'নূরা',                      ARRAY[25,   38,   26,   45.5, 34  ]),
('TSC0042', 'কে এম মুমতাহিম রশিদ',      ARRAY[33,   30,   24,   44,   38  ]),
('TSC0043', 'উম্মে মুবাসশিরা ফুল',       ARRAY[40,   48,   28,   49,   40  ]),
('TSC0044', 'গালিব',                     ARRAY[36.5, 42,   28,   47,   37  ]),
('TSC0045', 'আতকিয়া নাবিলা নাবা',      ARRAY[24.5, 35,   27,   NULL, 38  ]),
('TSC0046', 'জান্নাতুল মাওয়া সাফা',     ARRAY[NULL, NULL, 20,   36.5, 14  ]),
('TSC0047', 'তাওসিফ',                    ARRAY[17,   19,   17,   27,   24  ]),
('TSC0048', 'সালমান ফারসি',              ARRAY[28,   35.5, 26,   38,   37  ]),
('TSC0049', 'রিয়াজুল জান্নাত সুবহা',    ARRAY[16,   32,   27,   46,   36  ]),
('TSC0050', 'নওসিন তাবাসসুম',           ARRAY[31,   39,   28,   47.5, 38  ]),
('TSC0051', 'মুহতাসিম মাহদি',            ARRAY[32,   38.5, 27,   48,   34  ]),
('TSC0052', 'মাহদি হাসান',               ARRAY[NULL, 20,   NULL, 30,   12  ]),
('TSC0053', 'ইকরা সাবা',                 ARRAY[23.5, 34,   28,   49,   32  ]),
('TSC0054', 'রত্ন সরকার',                ARRAY[9,    19,   24,   19,   20  ]),
('TSC0055', 'শ্রেষ্ঠা',                   ARRAY[33,   44,   28,   46,   38  ]),
('TSC0056', 'ওহী',                        ARRAY[15,   20.5, 25,   29,   17  ]),
('TSC0057', 'আফিয়া',                     ARRAY[NULL, 46,   28,   48,   39  ]),
('TSC0058', 'জাইম',                       ARRAY[NULL, 21,   NULL, 34,   38  ]),
('TSC0059', 'হামিম',                      ARRAY[NULL, 25,   25,   NULL, 20  ]),
('TSC0060', 'জান্নাতুল মাওয়া মাহী',      ARRAY[NULL, 32,   28,   37,   NULL]),
('TSC0061', 'সোয়ায়েব',                  ARRAY[NULL, NULL, NULL, 33,   25  ]),
('TSC0062', 'মোমিন',                      ARRAY[NULL, NULL, NULL, 28,   30  ]),
('TSC0063', 'মানসিভ',                     ARRAY[NULL, NULL, NULL, 46,   19  ]),
('TSC0064', 'মুহাইমিনুল',                 ARRAY[NULL, NULL, NULL, NULL, NULL]::numeric[]);

INSERT INTO "Students" ("StudentId", "FullName", "ClassLevel", "BatchId", "IsActive")
SELECT sheet2.student_id, sheet2.full_name, '3', b."Id", true
FROM sheet2
CROSS JOIN (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 3') b;

INSERT INTO "Marks" ("StudentId", "ExamId", "SubjectId", "Score")
SELECT st."Id", x."Id", s."Id", cell.score
FROM sheet2
JOIN "Students" st ON st."StudentId" = sheet2.student_id
CROSS JOIN LATERAL unnest(sheet2.scores) WITH ORDINALITY AS cell(score, week)
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
