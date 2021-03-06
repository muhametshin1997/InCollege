﻿namespace InCollege.Core.Data.Base
{
    public enum StatementType : byte
    {
        Middle,
        Exam,
        QualificationExam,
        StudyPractice,
        IndustrialPractice,
        CourceProject,
        Total,
        Other
    }

    public enum AccountType : byte
    {
        Guest,
        Student,
        Professor,
        DepartmentHead,
        Admin
    }

    public enum ChatRequestMode : byte
    {
        CheckMessages,
        Conversation,
        Friends,
        Send
    }

    public enum TechnicalMarkValue : sbyte
    {
        Absent = -1,
        Passed = -2,
        Unpassed = -3,
        Blank = -4
    }
}
