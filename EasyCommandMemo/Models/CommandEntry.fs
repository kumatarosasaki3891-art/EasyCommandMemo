namespace EasyCommandMemo.Models

type CommandEntry =
    { Id: int64
      Title: string
      Category: string
      Tags: string
      CommandText: string
      Description: string
      IsFavorite: bool
      CopyCount: int64
      UpdatedAt: string }
