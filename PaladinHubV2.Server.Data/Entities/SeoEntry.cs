using System.ComponentModel.DataAnnotations;
namespace PaladinHubV2.Server.Data.Entities;
public sealed class SeoEntry
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public int? PageId {get;set;}
 [MaxLength(2048)]public string Path {get;set;}="*";
 [MaxLength(200)]public string Title {get;set;}="";
 [MaxLength(500)]public string Description {get;set;}="";
 [MaxLength(2048)]public string CanonicalUrl {get;set;}="";
 [MaxLength(200)]public string SocialTitle {get;set;}="";
 [MaxLength(500)]public string SocialDescription {get;set;}="";
 [MaxLength(2048)]public string ImageUrl {get;set;}="";
 public bool? Index {get;set;}
 public bool? Follow {get;set;}
 public bool IsArchived {get;set;}
 public bool IsDeleted {get;set;}
 [ConcurrencyCheck]public int Version {get;set;}=1;
}
public sealed class SeoRevision
{
 public Guid Id {get;set;}=Guid.NewGuid();public Guid EntryId {get;set;}public int Version {get;set;}
 public string Action {get;set;}="";public string Actor {get;set;}="";public string Snapshot {get;set;}="";
 public DateTime CreatedAtUtc {get;set;}=DateTime.UtcNow;
}
