# Lateral Inspire

Lateral Inspire is a high-quality productivity and motivational app for professionals and personal growth enthusiasts. It is designed to help users define, track, and accomplish their goals, from the smallest daily habits to the biggest life projects, while staying inspired at every step.

UI link: https://lateral-inspire.vercel.app

How to run?

1. Have .net9 and docker installed.
2. Use release-dev branch to run the project
3. If aspire opens in a browser you're all good.



How to create migrations?

1. Open Package Manager Console (Tools -> Package Manager Console)
2. Run this command - Add-Migration aMigrationName -StartupProject Inspire.ApiServices -Project Inspire.DB
3. No need for update database, there's code doing that automatically (check DatabaseSeedService)
