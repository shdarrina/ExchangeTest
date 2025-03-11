create table dbo.TestCache
(
    Id                         nvarchar(449)  not null collate SQL_Latin1_General_CP1_CS_AS
        primary key
            with (fillfactor = 85),
    Value                      varbinary(max) not null,
    ExpiresAtTime              datetimeoffset not null,
    SlidingExpirationInSeconds bigint,
    AbsoluteExpiration         datetimeoffset
)
go

create index Index_ExpiresAtTime
    on dbo.TestCache (ExpiresAtTime)
    with (fillfactor = 85)
go
