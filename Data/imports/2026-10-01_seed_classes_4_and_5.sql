-- Run ONCE, AFTER the ClassesAndBatches migration.
--
-- The centre runs four classes -- 2, 3, 4 and 5 -- but only 2 and 3 have ever had students,
-- so only those two existed as rows. This adds the other two so the admission form can
-- enrol into them from day one.
--
-- It runs after the migration, not before, because Level does not exist as a column until
-- the migration adds it.
--
-- Fees: Class 4 and Class 5 both start at 1500, matching Class 3. Change them in the
-- Classes screen whenever the centre sets its real figures -- nothing here depends on the
-- number.
--
-- Run:  PGCLIENTENCODING=UTF8 psql -h localhost -U postgres -d coaching_center -f this.sql
-- Runs once, and is safe to re-run: ON CONFLICT leaves an existing row alone.

BEGIN;

INSERT INTO "SchoolClasses" ("Name", "Level", "MonthlyFee", "Description")
VALUES
    ('Class 4', 4, 1500,
     'Class 4 -- morning and afternoon branches combined; weekly results are ranked together.'),
    ('Class 5', 5, 1500,
     'Class 5 -- morning and afternoon branches combined; weekly results are ranked together.')
ON CONFLICT ("Name") DO NOTHING;

-- All four classes present, levels 2..5, none duplicated -- which is what the unique index
-- on Level guarantees, checked here so a bad seed is caught before it is committed.
DO $$
DECLARE
    n int;
    levels text;
BEGIN
    SELECT count(*), string_agg("Level"::text, ',' ORDER BY "Level")
    INTO n, levels
    FROM "SchoolClasses";

    IF levels IS DISTINCT FROM '2,3,4,5' THEN
        RAISE EXCEPTION 'Expected levels 2,3,4,5 -- found % (% row(s)).', levels, n;
    END IF;

    RAISE NOTICE 'Classes seeded. Levels present: %.', levels;
END $$;

COMMIT;
