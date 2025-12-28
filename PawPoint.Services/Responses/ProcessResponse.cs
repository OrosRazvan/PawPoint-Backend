using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PawPoint.Services.Responses
{
    public record ProcessResponse(
        int TotalInFile, 
        int TotalValid, 
        int RecordsSaved
        );

}
