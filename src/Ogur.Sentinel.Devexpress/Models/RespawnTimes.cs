using System;

namespace Ogur.Sentinel.Devexpress.Models;

public record RespawnTimes(
    DateTime Next10m,
    DateTime Next2h
);