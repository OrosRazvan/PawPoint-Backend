using PawPoint.Services.Interfaces;
using PawPoint.Services.Parsing;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;

namespace PawPoint.Services;

public class AssistantService : IAssistantService
{
    private readonly IIntentParser _intentParser;
    private readonly IAssistantDataService _assistantDataService;

    public AssistantService(
        IIntentParser intentParser,
        IAssistantDataService assistantDataService)
    {
        _intentParser = intentParser;
        _assistantDataService = assistantDataService;
    }

    public async Task<AssistantMessageResponse> ProcessMessageAsync(
        AssistantMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsed = _intentParser.Parse(request.Message);

        return parsed.IntentType switch
        {
            AssistantIntentType.Greeting => HandleGreeting(),
            AssistantIntentType.Help => HandleHelp(),
            AssistantIntentType.ListPets => await HandleListPets(request.UserId, cancellationToken),
            AssistantIntentType.UpcomingAppointments => await HandleUpcomingAppointments(request.UserId, parsed.DaysAhead, cancellationToken),
            AssistantIntentType.PetOverview => await HandlePetOverview(request.UserId, parsed.PetName, cancellationToken),
            AssistantIntentType.PetVaccinations => await HandlePetVaccinations(request.UserId, parsed.PetName, cancellationToken),
            AssistantIntentType.PetDewormings => await HandlePetDewormings(request.UserId, parsed.PetName, cancellationToken),
            AssistantIntentType.HealthOverview => await HandleHealthOverview(request.UserId, cancellationToken),
            AssistantIntentType.DueItems => await HandleDueItems(request.UserId, cancellationToken),
            AssistantIntentType.Recommendations => await HandleRecommendations(request.UserId, cancellationToken),
            AssistantIntentType.DueVaccinations => await HandleDueVaccinations(request.UserId, cancellationToken),
            AssistantIntentType.DueDewormings => await HandleDueDewormings(request.UserId, cancellationToken),
            _ => HandleUnknown()
        };
    }

    private AssistantMessageResponse HandleGreeting()
    {
        return new AssistantMessageResponse
        {
            Intent = "Greeting",
            ReplyKey = "assistant.greeting",
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.listPets" },
                new() { Key = "assistant.suggestions.upcomingAppointments" },
                new() { Key = "assistant.suggestions.healthOverview" },
                new() { Key = "assistant.suggestions.recommendations" }
            }
        };
    }

    private AssistantMessageResponse HandleHelp()
    {
        return new AssistantMessageResponse
        {
            Intent = "Help",
            ReplyKey = "assistant.help",
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.listPets" },
                new() { Key = "assistant.suggestions.upcomingAppointments" },
                new() { Key = "assistant.suggestions.dueVaccinations" },
                new() { Key = "assistant.suggestions.dueDewormings" }
            }
        };
    }

    private async Task<AssistantMessageResponse> HandleListPets(int userId, CancellationToken cancellationToken)
    {
        var pets = await _assistantDataService.GetPetsAsync(userId, cancellationToken);

        if (!pets.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "ListPets",
                ReplyKey = "assistant.noPets"
            };
        }

        return new AssistantMessageResponse
        {
            Intent = "ListPets",
            ReplyKey = "assistant.listPets",
            ReplyParams = new Dictionary<string, string>
            {
                ["petNames"] = string.Join(", ", pets.Select(p => p.Name))
            },
            Data = pets,
            Suggestions = pets
                .Take(4)
                .Select(p => new AssistantSuggestionResponse
                {
                    Key = "assistant.suggestions.petOverview",
                    Params = new Dictionary<string, string>
                    {
                        ["petName"] = p.Name
                    }
                })
                .ToList()
        };
    }

    private async Task<AssistantMessageResponse> HandleUpcomingAppointments(
        int userId,
        int daysAhead,
        CancellationToken cancellationToken)
    {
        var appointments = await _assistantDataService.GetUpcomingAppointmentsAsync(userId, daysAhead, cancellationToken);

        if (!appointments.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "UpcomingAppointments",
                ReplyKey = "assistant.noAppointments",
                ReplyParams = new Dictionary<string, string>
                {
                    ["days"] = daysAhead.ToString()
                }
            };
        }

        var appointmentLines = appointments
            .OrderBy(a => a.ScheduledAt)
            .Take(6)
            .Select(a =>
            {
                var baseText = $"{a.PetName} - {a.Type} - {a.ScheduledAt:dd.MM.yyyy HH:mm}";

                if (!string.IsNullOrWhiteSpace(a.VetCabinetName))
                    baseText += $" ({a.VetCabinetName})";

                return baseText;
            })
            .ToList();

        return new AssistantMessageResponse
        {
            Intent = "UpcomingAppointments",
            ReplyKey = "assistant.upcomingAppointments",
            ReplyParams = new Dictionary<string, string>
            {
                ["appointments"] = string.Join("; ", appointmentLines)
            },
            Data = appointments,
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.healthOverview" },
                new() { Key = "assistant.suggestions.dueVaccinations" },
                new() { Key = "assistant.suggestions.dueDewormings" }
            }
        };
    }

    private async Task<AssistantMessageResponse> HandlePetOverview(
        int userId,
        string? petName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(petName))
            return HandleUnknown();

        var pet = await _assistantDataService.GetPetByNameAsync(userId, petName, cancellationToken);

        if (pet is null)
        {
            return new AssistantMessageResponse
            {
                Intent = "PetOverview",
                ReplyKey = "assistant.petNotFound",
                ReplyParams = new Dictionary<string, string>
                {
                    ["petName"] = petName
                }
            };
        }

        var appointments = await _assistantDataService.GetAppointmentsForPetAsync(userId, pet.Id, cancellationToken);
        var vaccinations = await _assistantDataService.GetVaccinationsAsync(userId, pet.Id, cancellationToken);
        var dewormings = await _assistantDataService.GetDewormingsAsync(userId, pet.Id, cancellationToken);

        var nextAppointment = appointments
            .Where(a => a.ScheduledAt >= DateTime.UtcNow)
            .OrderBy(a => a.ScheduledAt)
            .FirstOrDefault();

        var nextVaccination = vaccinations
            .Where(v => v.NextDueDate.HasValue)
            .OrderBy(v => v.NextDueDate)
            .FirstOrDefault();

        var nextDeworming = dewormings
            .Where(d => d.NextDueDate.HasValue)
            .OrderBy(d => d.NextDueDate)
            .FirstOrDefault();

        var segments = new List<string>();

        if (string.IsNullOrWhiteSpace(pet.Breed))
        {
            segments.Add($"assistant.petOverview.base|petName={pet.Name}|species={pet.Species}");
        }
        else
        {
            segments.Add($"assistant.petOverview.withBreed|petName={pet.Name}|species={pet.Species}|breed={pet.Breed}");
        }

        if (nextAppointment is not null)
        {
            segments.Add($"assistant.petOverview.nextAppointment|date={nextAppointment.ScheduledAt:dd.MM.yyyy HH:mm}|type={nextAppointment.Type}");
        }

        if (nextVaccination?.NextDueDate is not null)
        {
            segments.Add($"assistant.petOverview.nextVaccination|date={nextVaccination.NextDueDate.Value:dd.MM.yyyy}");
        }

        if (nextDeworming?.NextDueDate is not null)
        {
            segments.Add($"assistant.petOverview.nextDeworming|date={nextDeworming.NextDueDate.Value:dd.MM.yyyy}");
        }

        return new AssistantMessageResponse
        {
            Intent = "PetOverview",
            ReplyKey = "assistant.petOverview.composed",
            ReplyParams = new Dictionary<string, string>
            {
                ["segments"] = string.Join("||", segments)
            },
            Data = new
            {
                pet,
                nextAppointment,
                nextVaccination,
                nextDeworming
            },
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new()
                {
                    Key = "assistant.suggestions.petVaccinations",
                    Params = new Dictionary<string, string>
                    {
                        ["petName"] = pet.Name
                    }
                },
                new()
                {
                    Key = "assistant.suggestions.petDewormings",
                    Params = new Dictionary<string, string>
                    {
                        ["petName"] = pet.Name
                    }
                },
                new()
                {
                    Key = "assistant.suggestions.petRecommendations",
                    Params = new Dictionary<string, string>
                    {
                        ["petName"] = pet.Name
                    }
                }
            }
        };
    }

    private async Task<AssistantMessageResponse> HandlePetVaccinations(
        int userId,
        string? petName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(petName))
            return HandleUnknown();

        var pet = await _assistantDataService.GetPetByNameAsync(userId, petName, cancellationToken);

        if (pet is null)
        {
            return new AssistantMessageResponse
            {
                Intent = "PetVaccinations",
                ReplyKey = "assistant.petNotFound",
                ReplyParams = new Dictionary<string, string>
                {
                    ["petName"] = petName
                }
            };
        }

        var vaccinations = await _assistantDataService.GetVaccinationsAsync(userId, pet.Id, cancellationToken);

        if (!vaccinations.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "PetVaccinations",
                ReplyKey = "assistant.noVaccinations",
                ReplyParams = new Dictionary<string, string>
                {
                    ["petName"] = pet.Name
                }
            };
        }

        var due = vaccinations
            .Where(v => v.NextDueDate.HasValue)
            .OrderBy(v => v.NextDueDate)
            .FirstOrDefault();

        return new AssistantMessageResponse
        {
            Intent = "PetVaccinations",
            ReplyKey = due is null
                ? "assistant.petVaccinations.noDueDate"
                : "assistant.petVaccinations.nextDue",
            ReplyParams = due is null
                ? new Dictionary<string, string>
                {
                    ["count"] = vaccinations.Count.ToString(),
                    ["petName"] = pet.Name
                }
                : new Dictionary<string, string>
                {
                    ["petName"] = pet.Name,
                    ["date"] = due.NextDueDate!.Value.ToString("dd.MM.yyyy")
                },
            Data = vaccinations
        };
    }

    private async Task<AssistantMessageResponse> HandlePetDewormings(
        int userId,
        string? petName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(petName))
            return HandleUnknown();

        var pet = await _assistantDataService.GetPetByNameAsync(userId, petName, cancellationToken);

        if (pet is null)
        {
            return new AssistantMessageResponse
            {
                Intent = "PetDewormings",
                ReplyKey = "assistant.petNotFound",
                ReplyParams = new Dictionary<string, string>
                {
                    ["petName"] = petName
                }
            };
        }

        var dewormings = await _assistantDataService.GetDewormingsAsync(userId, pet.Id, cancellationToken);

        if (!dewormings.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "PetDewormings",
                ReplyKey = "assistant.noDewormings",
                ReplyParams = new Dictionary<string, string>
                {
                    ["petName"] = pet.Name
                }
            };
        }

        var due = dewormings
            .Where(d => d.NextDueDate.HasValue)
            .OrderBy(d => d.NextDueDate)
            .FirstOrDefault();

        return new AssistantMessageResponse
        {
            Intent = "PetDewormings",
            ReplyKey = due is null
                ? "assistant.petDewormings.noDueDate"
                : "assistant.petDewormings.nextDue",
            ReplyParams = due is null
                ? new Dictionary<string, string>
                {
                    ["count"] = dewormings.Count.ToString(),
                    ["petName"] = pet.Name
                }
                : new Dictionary<string, string>
                {
                    ["petName"] = pet.Name,
                    ["date"] = due.NextDueDate!.Value.ToString("dd.MM.yyyy")
                },
            Data = dewormings
        };
    }

    private async Task<AssistantMessageResponse> HandleHealthOverview(
        int userId,
        CancellationToken cancellationToken)
    {
        var pets = await _assistantDataService.GetPetsAsync(userId, cancellationToken);
        var appointments = await _assistantDataService.GetUpcomingAppointmentsAsync(userId, 30, cancellationToken);
        var vaccinations = await _assistantDataService.GetVaccinationsAsync(userId, null, cancellationToken);
        var dewormings = await _assistantDataService.GetDewormingsAsync(userId, null, cancellationToken);

        var today = DateTime.UtcNow.Date;

        var dueVaccinations = vaccinations.Count(v =>
            v.NextDueDate.HasValue && v.NextDueDate.Value.Date <= today.AddDays(7));

        var dueDewormings = dewormings.Count(d =>
            d.NextDueDate.HasValue && d.NextDueDate.Value.Date <= today.AddDays(7));

        var alertKeys = new List<string>();

        if (dueVaccinations > 0)
            alertKeys.Add($"assistant.alerts.dueVaccinations|count={dueVaccinations}");

        if (dueDewormings > 0)
            alertKeys.Add($"assistant.alerts.dueDewormings|count={dueDewormings}");

        if (appointments.Count > 0)
            alertKeys.Add($"assistant.alerts.appointments|count={appointments.Count}");

        return new AssistantMessageResponse
        {
            Intent = "HealthOverview",
            ReplyKey = "assistant.healthOverview",
            ReplyParams = new Dictionary<string, string>
            {
                ["petCount"] = pets.Count.ToString(),
                ["appointmentCount"] = appointments.Count.ToString(),
                ["dueVaccinations"] = dueVaccinations.ToString(),
                ["dueDewormings"] = dueDewormings.ToString()
            },
            Data = new
            {
                TotalPets = pets.Count,
                UpcomingAppointments = appointments.Count,
                DueVaccinations = dueVaccinations,
                DueDewormings = dueDewormings,
                AlertKeys = alertKeys
            },
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.upcomingAppointments" },
                new() { Key = "assistant.suggestions.dueVaccinations" },
                new() { Key = "assistant.suggestions.dueDewormings" },
                new() { Key = "assistant.suggestions.recommendations" }
            }
        };
    }

    private async Task<AssistantMessageResponse> HandleDueItems(
        int userId,
        CancellationToken cancellationToken)
    {
        var vaccinations = await _assistantDataService.GetVaccinationsAsync(userId, null, cancellationToken);
        var dewormings = await _assistantDataService.GetDewormingsAsync(userId, null, cancellationToken);

        var today = DateTime.UtcNow.Date;

        var dueVaccinations = vaccinations
            .Where(v => v.NextDueDate.HasValue && v.NextDueDate.Value.Date <= today.AddDays(14))
            .OrderBy(v => v.NextDueDate)
            .Take(10)
            .ToList();

        var dueDewormings = dewormings
            .Where(d => d.NextDueDate.HasValue && d.NextDueDate.Value.Date <= today.AddDays(14))
            .OrderBy(d => d.NextDueDate)
            .Take(10)
            .ToList();

        if (!dueVaccinations.Any() && !dueDewormings.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "DueItems",
                ReplyKey = "assistant.noDueItems"
            };
        }

        return new AssistantMessageResponse
        {
            Intent = "DueItems",
            ReplyKey = "assistant.dueItems.summary",
            ReplyParams = new Dictionary<string, string>
            {
                ["vaccinations"] = string.Join("; ", dueVaccinations.Select(v => $"{v.PetName} - {v.NextDueDate:dd.MM.yyyy}")),
                ["dewormings"] = string.Join("; ", dueDewormings.Select(d => $"{d.PetName} - {d.NextDueDate:dd.MM.yyyy}"))
            },
            Data = new
            {
                dueVaccinations,
                dueDewormings
            }
        };
    }

    private async Task<AssistantMessageResponse> HandleRecommendations(
        int userId,
        CancellationToken cancellationToken)
    {
        var pets = await _assistantDataService.GetPetsAsync(userId, cancellationToken);
        var appointments = await _assistantDataService.GetUpcomingAppointmentsAsync(userId, 30, cancellationToken);
        var vaccinations = await _assistantDataService.GetVaccinationsAsync(userId, null, cancellationToken);
        var dewormings = await _assistantDataService.GetDewormingsAsync(userId, null, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var recommendationKeys = new List<string>();

        var overdueVaccinations = vaccinations
            .Where(v => v.NextDueDate.HasValue && v.NextDueDate.Value.Date < today)
            .ToList();

        var overdueDewormings = dewormings
            .Where(d => d.NextDueDate.HasValue && d.NextDueDate.Value.Date < today)
            .ToList();

        if (overdueVaccinations.Any())
            recommendationKeys.Add($"assistant.recommendations.overdueVaccinations|count={overdueVaccinations.Count}");

        if (overdueDewormings.Any())
            recommendationKeys.Add($"assistant.recommendations.overdueDewormings|count={overdueDewormings.Count}");

        if (!appointments.Any() && pets.Any())
            recommendationKeys.Add("assistant.recommendations.noAppointments");

        foreach (var pet in pets.Take(3))
        {
            recommendationKeys.Add($"assistant.recommendations.checkPet|petName={pet.Name}");
        }

        if (!recommendationKeys.Any())
            recommendationKeys.Add("assistant.recommendations.allGood");

        return new AssistantMessageResponse
        {
            Intent = "Recommendations",
            ReplyKey = "assistant.recommendations.summary",
            ReplyParams = new Dictionary<string, string>
            {
                ["items"] = string.Join("||", recommendationKeys)
            },
            Data = new
            {
                RecommendationKeys = recommendationKeys
            },
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.dueVaccinations" },
                new() { Key = "assistant.suggestions.dueDewormings" },
                new() { Key = "assistant.suggestions.upcomingAppointments" }
            }
        };
    }

    private AssistantMessageResponse HandleUnknown()
    {
        return new AssistantMessageResponse
        {
            Intent = "Unknown",
            ReplyKey = "assistant.unknown",
            Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.listPets" },
                new() { Key = "assistant.suggestions.upcomingAppointments" },
                new() { Key = "assistant.suggestions.dueVaccinations" },
                new() { Key = "assistant.suggestions.recommendations" }
            }
        };
    }

    private async Task<AssistantMessageResponse> HandleDueVaccinations(
    int userId,
    CancellationToken cancellationToken)
    {
        var vaccinations = await _assistantDataService.GetVaccinationsAsync(userId, null, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var scheduledVaccinations = vaccinations
            .Where(v => v.ScheduledAt.HasValue && v.ScheduledAt.Value >= now)
            .OrderBy(v => v.ScheduledAt)
            .Take(10)
            .ToList();

        if (scheduledVaccinations.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "DueVaccinations",
                ReplyKey = "assistant.scheduledVaccinations",
                ReplyParams = new Dictionary<string, string>
                {
                    ["vaccinations"] = string.Join("; ", scheduledVaccinations.Select(v =>
                        $"{v.PetName} - {v.VaccineName} - {v.ScheduledAt:dd.MM.yyyy HH:mm}"))
                },
                Data = scheduledVaccinations,
                Suggestions = new List<AssistantSuggestionResponse>
            {
                new() { Key = "assistant.suggestions.dueDewormings" },
                new() { Key = "assistant.suggestions.upcomingAppointments" },
                new() { Key = "assistant.suggestions.recommendations" }
            }
            };
        }

        var dueVaccinations = vaccinations
            .Where(v => v.NextDueDate.HasValue && v.NextDueDate.Value.Date <= today.AddDays(14))
            .OrderBy(v => v.NextDueDate)
            .Take(10)
            .ToList();

        if (!dueVaccinations.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "DueVaccinations",
                ReplyKey = "assistant.noDueVaccinations"
            };
        }

        return new AssistantMessageResponse
        {
            Intent = "DueVaccinations",
            ReplyKey = "assistant.dueVaccinations",
            ReplyParams = new Dictionary<string, string>
            {
                ["vaccinations"] = string.Join("; ", dueVaccinations.Select(v =>
                    $"{v.PetName} - {v.VaccineName} - {v.NextDueDate:dd.MM.yyyy}"))
            },
            Data = dueVaccinations
        };
    }

    private async Task<AssistantMessageResponse> HandleDueDewormings(
    int userId,
    CancellationToken cancellationToken)
    {
        var dewormings = await _assistantDataService.GetDewormingsAsync(userId, null, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var scheduledDewormings = dewormings
            .Where(d => d.ScheduledAt.HasValue && d.ScheduledAt.Value >= now)
            .OrderBy(d => d.ScheduledAt)
            .Take(10)
            .ToList();

        if (scheduledDewormings.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "DueDewormings",
                ReplyKey = "assistant.scheduledDewormings",
                ReplyParams = new Dictionary<string, string>
                {
                    ["dewormings"] = string.Join("; ", scheduledDewormings.Select(d =>
                        $"{d.PetName} - {d.DewormingType} - {d.ScheduledAt:dd.MM.yyyy}"))
                },
                Data = scheduledDewormings
            };
        }

        var dueDewormings = dewormings
            .Where(d => d.NextDueDate.HasValue && d.NextDueDate.Value.Date <= today.AddDays(14))
            .OrderBy(d => d.NextDueDate)
            .Take(10)
            .ToList();

        if (!dueDewormings.Any())
        {
            return new AssistantMessageResponse
            {
                Intent = "DueDewormings",
                ReplyKey = "assistant.noDueDewormings"
            };
        }

        return new AssistantMessageResponse
        {
            Intent = "DueDewormings",
            ReplyKey = "assistant.dueDewormings",
            ReplyParams = new Dictionary<string, string>
            {
                ["dewormings"] = string.Join("; ", dueDewormings.Select(d =>
                    $"{d.PetName} - {d.DewormingType} - {d.NextDueDate:dd.MM.yyyy}"))
            },
            Data = dueDewormings
        };
    }
}


