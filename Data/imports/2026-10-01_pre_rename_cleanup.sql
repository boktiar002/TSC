-- One-off cleanup, run ONCE before the ClassesAndBatches migration.
--
-- The Batches table had grown into a mix of two concepts: two real classes ('Class 2',
-- 'Class 3') alongside two half-built shift rows ('Morning', 'Afternoon'). The migration
-- derives SchoolClasses.Level from the class name and refuses to complete if any class is
-- left at 0, so Morning and Afternoon have to go first.
--
-- This is data, not schema, so it is deliberately NOT inside the migration: a schema
-- migration should not delete a student record, and the migration's guard failing loudly
-- is the correct outcome if this script was skipped.
--
-- What goes, and why:
--   * Student TSC0501 'Has History' -- a record made to test that archiving blocks
--     deletion. Its one 1500 payment goes with it (Payments cascade from Students).
--   * Exam 'CT2' on the Afternoon row -- 0 papers, 0 marks. An empty stub.
--   * The Morning and Afternoon rows themselves.
-- What moves:
--   * Turag Hasan's Mathematics assignment, from Afternoon to Class 3.
--
-- Everything real is untouched: 87 students, 409 marks, 72 attendance rows across
-- Class 2 (24 students) and Class 3 (63 students).
--
-- Run:  PGCLIENTENCODING=UTF8 psql -h localhost -U postgres -d coaching_center -f this.sql
-- Runs once. A second run is a no-op: every statement is keyed on rows that no longer exist.

BEGIN;

-- Resolve the rows by name rather than by hard-coded Id, so this is safe to run against
-- the rehearsal clone and the real database alike.
CREATE TEMP TABLE _ids AS
SELECT
    (SELECT "Id" FROM "Batches" WHERE "Name" = 'Morning')   AS morning_id,
    (SELECT "Id" FROM "Batches" WHERE "Name" = 'Afternoon') AS afternoon_id,
    (SELECT "Id" FROM "Batches" WHERE "Name" = 'Class 3')   AS class3_id;

-- Refuse to run if the shape is not what we surveyed. Better a loud stop than a guess
-- against a database that has moved on since.
DO $$
DECLARE
    n_morning   int;
    n_afternoon int;
    n_class3    int;
BEGIN
    SELECT count(*) INTO n_morning   FROM "Batches" WHERE "Name" = 'Morning';
    SELECT count(*) INTO n_afternoon FROM "Batches" WHERE "Name" = 'Afternoon';
    SELECT count(*) INTO n_class3    FROM "Batches" WHERE "Name" = 'Class 3';

    IF n_class3 <> 1 THEN
        RAISE EXCEPTION 'Expected exactly one Class 3 row, found %. Stopping.', n_class3;
    END IF;

    -- Already cleaned up: nothing to do, and that is fine.
    IF n_morning = 0 AND n_afternoon = 0 THEN
        RAISE NOTICE 'Morning and Afternoon rows are already gone. Nothing to clean.';
    END IF;

    -- Any student in Morning or Afternoon other than the known test record means real
    -- children have been enrolled into a shift row. That needs a person, not a script.
    IF EXISTS (
        SELECT 1 FROM "Students" s
        JOIN "Batches" b ON b."Id" = s."BatchId"
        WHERE b."Name" IN ('Morning', 'Afternoon')
          AND s."StudentId" <> 'TSC0501'
    ) THEN
        RAISE EXCEPTION
            'A student other than the TSC0501 test record sits in Morning or Afternoon. '
            'Move them to a real class first -- this script will not decide where they belong.';
    END IF;
END $$;

-- 1. Move the teacher assignment off Afternoon before that row disappears.
UPDATE "BatchSubjects"
SET "BatchId" = (SELECT class3_id FROM _ids)
WHERE "BatchId" = (SELECT afternoon_id FROM _ids);

-- 2. Drop the empty stub exams on the shift rows. Confirmed 0 papers and 0 marks; the
--    conditions re-check that rather than trusting the survey.
DELETE FROM "Exams" e
WHERE e."BatchId" IN (SELECT morning_id FROM _ids UNION SELECT afternoon_id FROM _ids)
  AND NOT EXISTS (SELECT 1 FROM "ExamSubjects" es WHERE es."ExamId" = e."Id")
  AND NOT EXISTS (SELECT 1 FROM "Marks" m WHERE m."ExamId" = e."Id");

-- 3. Drop the test student. Marks, attendance and payments cascade from Students.
DELETE FROM "Students" WHERE "StudentId" = 'TSC0501';

-- 4. Drop the shift rows themselves. This fails loudly if anything still references them,
--    which is the point -- a silent cascade here would take real records with it.
DELETE FROM "Batches" WHERE "Name" IN ('Morning', 'Afternoon');

-- 5. Prove the result before committing: only real classes left, every one of them
--    carrying its number in the name so the migration's backfill can read it.
DO $$
DECLARE
    bad int;
    remaining int;
BEGIN
    SELECT count(*) INTO bad FROM "Batches" WHERE "Name" !~ '[0-9]';
    IF bad > 0 THEN
        RAISE EXCEPTION '% class row(s) still have no number in the name.', bad;
    END IF;

    SELECT count(*) INTO remaining FROM "Batches";
    RAISE NOTICE 'Cleanup done. % class row(s) remain.', remaining;
END $$;

COMMIT;
