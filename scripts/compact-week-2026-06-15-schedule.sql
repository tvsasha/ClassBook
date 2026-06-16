SET NOCOUNT ON;
SET DATEFIRST 7;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @WeekStart DATE = '2026-06-15';
DECLARE @WeekEnd DATE = DATEADD(DAY, 7, @WeekStart);

;WITH OrderedLessons AS
(
    SELECT
        l.LessonId,
        CAST(l.[Date] AS DATE) AS LessonDate,
        l.ClassId,
        ROW_NUMBER() OVER
        (
            PARTITION BY CAST(l.[Date] AS DATE), l.ClassId
            ORDER BY
                CASE WHEN currentSlot.ScheduleId IS NULL THEN 999 ELSE currentSlot.LessonNumber END,
                l.LessonId
        ) AS RowNumber
    FROM Lessons l
    LEFT JOIN Schedules currentSlot ON currentSlot.ScheduleId = l.ScheduleId
    WHERE l.[Date] >= @WeekStart
      AND l.[Date] < @WeekEnd
),
TargetSlots AS
(
    SELECT
        ordered.LessonId,
        targetSlot.ScheduleId
    FROM OrderedLessons ordered
    INNER JOIN Schedules targetSlot
        ON targetSlot.DayOfWeek = DATEPART(WEEKDAY, ordered.LessonDate) - 2
       AND targetSlot.LessonNumber = ordered.RowNumber
)
UPDATE lesson
SET ScheduleId = target.ScheduleId
FROM Lessons lesson
INNER JOIN TargetSlots target ON target.LessonId = lesson.LessonId;
