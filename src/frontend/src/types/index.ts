export interface Supermarket {
  Id: string;
  Name: string;
  Slug: string;
  WebsiteUrl: string;
  IntegrationStatus: 'active' | 'blocked' | 'maintenance' | 'pending';
  StatusReason?: string | null;
  CreatedAt?: string;
}

export interface PriceHistoryPoint {
  Price: number;
  OriginalPrice: number | null;
  ObservedAt: string;
}

export interface StoreOffer {
  StoreProductId: string;
  SupermarketId: string;
  SupermarketName: string;
  SupermarketSlug: string;
  SourceProductId: string;
  SourceProductName: string;
  ProductUrl: string;
  ImageUrl?: string | null;
  AvailabilityStatus: 'in_stock' | 'out_of_stock' | 'unknown';
  LastObservedAt: string;
  CurrentPrice: number;
  OriginalPrice: number | null;
  Currency: string;
  PriceHistory: PriceHistoryPoint[];
}

export interface VerifiedProduct {
  Id: string;
  CanonicalName: string;
  Brand: string;
  Barcode?: string | null;
  Quantity: number;
  Unit: string;
  Category: string;
  OffersCount: number;
  MinPrice: number;
  MaxPrice: number;
  PriceDifference: number;
  CheapestSupermarket?: string | null;
  Offers: StoreOffer[];
}

export interface CollectorRun {
  Id: string;
  SupermarketId: string;
  StartedAt: string;
  FinishedAt?: string | null;
  Status: 'running' | 'success' | 'failed' | 'blocked';
  ProductsChecked: number;
  ProductsUpdated: number;
  ErrorMessage?: string | null;
}
