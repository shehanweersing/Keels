import React, { useState, useEffect } from 'react';
import type { VerifiedProduct, Supermarket, CollectorRun } from './types';
import { fetchVerifiedCatalog, fetchSupermarkets, fetchCollectorRuns } from './lib/supabase';
import { Navbar } from './components/Navbar';
import { Footer } from './components/Footer';
import { HomePage } from './pages/HomePage';
import { SearchPage } from './pages/SearchPage';
import { ComparisonPage } from './pages/ComparisonPage';
import { DetailsPage } from './pages/DetailsPage';
import { StatusPage } from './pages/StatusPage';
import { Loader2 } from 'lucide-react';

export const App: React.FC = () => {
  const [currentPage, setCurrentPage] = useState<string>('home');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedProduct, setSelectedProduct] = useState<VerifiedProduct | null>(null);

  const [products, setProducts] = useState<VerifiedProduct[]>([]);
  const [supermarkets, setSupermarkets] = useState<Supermarket[]>([]);
  const [collectorRuns, setCollectorRuns] = useState<CollectorRun[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [lastUpdated, setLastUpdated] = useState<string | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const [catalogData, superData, runsData] = await Promise.all([
        fetchVerifiedCatalog(),
        fetchSupermarkets(),
        fetchCollectorRuns()
      ]);

      setProducts(catalogData);
      setSupermarkets(superData);
      setCollectorRuns(runsData);

      if (catalogData.length > 0 && catalogData[0].Offers.length > 0) {
        setLastUpdated(catalogData[0].Offers[0].LastObservedAt);
      }
    } catch (err) {
      console.error('Failed to load application data:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleNavigate = (page: string, params?: any) => {
    if (params?.query) {
      setSearchQuery(params.query);
    }
    setCurrentPage(page);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const handleSelectProduct = (product: VerifiedProduct) => {
    setSelectedProduct(product);
    setCurrentPage('details');
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const handleSearchFromHome = (query: string) => {
    setSearchQuery(query);
    setCurrentPage('search');
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 text-slate-900">
      <Navbar
        currentPage={currentPage}
        onNavigate={handleNavigate}
        freshnessTimestamp={lastUpdated}
      />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 pt-8">
        {loading ? (
          <div className="flex flex-col items-center justify-center py-32 space-y-4">
            <Loader2 className="w-8 h-8 text-emerald-600 animate-spin" />
            <p className="text-sm font-medium text-slate-500">Loading verified price intelligence...</p>
          </div>
        ) : (
          <>
            {currentPage === 'home' && (
              <HomePage
                products={products}
                supermarkets={supermarkets}
                onSearch={handleSearchFromHome}
                onSelectProduct={handleSelectProduct}
                onNavigate={handleNavigate}
              />
            )}

            {currentPage === 'search' && (
              <SearchPage
                products={products}
                supermarkets={supermarkets}
                initialQuery={searchQuery}
                onSelectProduct={handleSelectProduct}
              />
            )}

            {currentPage === 'compare' && (
              <ComparisonPage
                products={products}
                supermarkets={supermarkets}
                onSelectProduct={handleSelectProduct}
              />
            )}

            {currentPage === 'details' && selectedProduct && (
              <DetailsPage
                product={selectedProduct}
                onBack={() => handleNavigate('search')}
              />
            )}

            {currentPage === 'status' && (
              <StatusPage
                supermarkets={supermarkets}
                collectorRuns={collectorRuns}
                onRefresh={loadData}
              />
            )}
          </>
        )}
      </main>

      <Footer />
    </div>
  );
};
