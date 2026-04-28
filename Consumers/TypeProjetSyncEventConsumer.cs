using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;   // <--- AJOUTER CETTE LIGNE C'EST CRUCIAL
using GestionProjet.Models;

namespace GestionProjet.Consumers
{
    public class TypeProjetSyncEventConsumer : IConsumer<TypeProjetSyncEvent>
    {
        private readonly ApplicationDbContext _db;

        public TypeProjetSyncEventConsumer(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<TypeProjetSyncEvent> ctx)
        {
            var evt = ctx.Message;
            
            var existing = await _db.TypesProjet
                .FirstOrDefaultAsync(t => t.TypeProjetGuid == evt.TypeProjetGuid);

            if (evt.Action == SyncAction.Deleted)
            {
                if (existing != null) 
                {
                    _db.TypesProjet.Remove(existing);
                }
            }
            else
            {
                if (existing == null)
                {
                    _db.TypesProjet.Add(new TypeProjet
                    {
                        TypeProjetGuid = evt.TypeProjetGuid,
                        Value = evt.Value,
                        Label = evt.Label,
                        IsActive = evt.IsActive,
                        Ordre = evt.Ordre,
                        UpdatedAt = evt.ChangedAt,
                    });
                }
                else if (evt.ChangedAt >= existing.UpdatedAt)
                {
                    existing.Value = evt.Value;
                    existing.Label = evt.Label;
                    existing.IsActive = evt.IsActive;
                    existing.Ordre = evt.Ordre;
                    existing.UpdatedAt = evt.ChangedAt;
                    
                    _db.TypesProjet.Update(existing);
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}