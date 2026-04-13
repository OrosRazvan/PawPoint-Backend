namespace PawPoint.DB.Enums
{
    public enum NotificationTypeEnum
    {
        AppointmentBooked = 1,
        AppointmentReminder = 2,
        AppointmentRescheduled = 3,
        AppointmentCancelled = 4,

        VaccinationBooked = 10,
        VaccinationReminder = 11,
        VaccinationDue = 12,
        VaccinationUpdated = 13,
        VaccinationCancelled = 14,

        DewormingBooked = 20,
        DewormingReminder = 21,
        DewormingDue = 22,
        DewormingUpdated = 23,
        DewormingCancelled = 24,

        FeedingReminder = 30,

        AnimalCreated = 40,
        AnimalUpdated = 41,
        AnimalDeleted = 42,

        ProfileUpdated = 50,
        PasswordChanged = 51,
        SettingsUpdated = 52
    }
}