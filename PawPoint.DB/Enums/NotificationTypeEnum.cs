namespace PawPoint.DB.Enums
{
    public enum NotificationTypeEnum
    {
        AppointmentBooked = 1,
        AppointmentReminder = 2,
        AppointmentRescheduled = 3,

        VaccinationBooked = 10,   
        VaccinationReminder = 11, 
        VaccinationDue = 12,     

        DewormingBooked = 20,
        DewormingReminder = 21,
        DewormingDue = 22,

        FeedingReminder = 30,
    }
}
