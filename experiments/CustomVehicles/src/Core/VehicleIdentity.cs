using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace RallyCustomVehicles
{
    public sealed class VehicleIdentity
    {
        public int identitySchema=1, packageSchema, physicsSchema=1;
        public string id, version, packageHash, physicsHash, donorPrefab;
        public bool physicsApplied;
        public static VehicleIdentity From(VehiclePackage p, bool applied=false)
        {
            using(var sha=SHA256.Create())
                return new VehicleIdentity { packageSchema=p.Manifest.schemaVersion, id=p.Manifest.id, version=p.Manifest.version,
                    donorPrefab=p.Manifest.donorPrefab, packageHash=p.ContentHash, physicsApplied=applied,
                    physicsHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(p.Manifest.physics)))).Replace("-","").ToLowerInvariant() };
        }
        public bool Matches(VehicleIdentity other) => other!=null && identitySchema==other.identitySchema && packageSchema==other.packageSchema && physicsSchema==other.physicsSchema &&
            id==other.id && version==other.version && packageHash==other.packageHash && physicsHash==other.physicsHash && donorPrefab==other.donorPrefab && physicsApplied==other.physicsApplied;
    }
}
